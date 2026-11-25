using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Reflection;
using Bussiness;
using Bussiness.CenterService;
using Bussiness.Managers;
using Bussiness.Protocol;
using Game.Base.Config;
using log4net;
using SqlDataProvider.Data;

namespace GameAdmin.Web
{
    /// <summary>
    /// The page methods of GameAdmin/Admin/mainRequest.ashx.cs (jQuery POSTs to mainRequest.ashx/&lt;Method&gt;),
    /// ported to this repo's Bussiness API. Each public static method here can be called by name;
    /// they return HTML fragments that the pages insert as they are.
    /// Changes from the original:
    ///   - PlayerBussiness.SendMail(mail) and WorldEventMgr.SendItemsToMail(..., title, content) take no
    ///     server/agent ID here. SendMail also tells the center server, so online players get the mail.
    ///   - The receiver can be a user name, a nickname or a character ID for both SendMoney and SendMailByAdmin.
    ///   - Text typed into the page is HTML-encoded before it is echoed back.
    /// </summary>
    public static class AdminMethods
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static int Setting(string key, int fallback)
        {
            return int.TryParse(ConfigurationManager.AppSettings[key], out int value) ? value : fallback;
        }

        private static string Encode(string text)
        {
            return WebUtility.HtmlEncode(text ?? "");
        }

        /// <summary>The original "Thêm vào / Xoá / Chỉnh sửa" links: never implemented, they show the "not built yet" panel.</summary>
        public static string SelectFunc(int request, int requestID)
        {
            return request >= 1 && request <= 3 ? NotBuiltYet() : "";
        }

        public static string Search_Item(int GoodsID)
        {
            using ProduceBussiness business = new ProduceBussiness();
            ItemTemplateInfo item = business.GetSingleGoods(GoodsID);
            if (item == null)
            {
                return "Không tìm thấy Item có ID: " + GoodsID;
            }
            return "<table ><tr>"
                + ItemHtml.MidTable(item.Name, item.CategoryID, item.TemplateID, ItemHtml.IconUrl(item).ToLower(), item.Pic)
                + "</tr></table>";
        }

        public static string SendMoney(string UserName, int Gold, int Money, int GiftToken)
        {
            using PlayerBussiness business = new PlayerBussiness();
            PlayerInfo player = FindPlayer(business, UserName);
            if (player == null)
            {
                return "Tài khoản <strong>" + Encode(UserName) + "</strong> không tồn tại.";
            }
            MailInfo mail = new MailInfo
            {
                Title = "Send Money, Gold, Gif Token",
                Content = "Bạn nhận được " + Money + " Xu, " + Gold + " Vàng, " + GiftToken + " Lễ kim",
                ReceiverID = player.ID,
                Receiver = player.NickName,
                Sender = "Administrators",
                SenderID = 0,
                Gold = Gold,
                Money = Money,
                GiftToken = GiftToken
            };
            if (!business.SendMail(mail))
            {
                return "Gửi thất bại!";
            }
            log.InfoFormat("SendMoney to {0} ({1}): {2} Xu, {3} Vàng, {4} Lễ kim", player.NickName, player.ID, Money, Gold, GiftToken);
            return "Đã chuyển thành công <br /> " + Money + " Xu <br /> " + Gold + " Vàng <br /> " + GiftToken
                + " Lễ kim <br /> cho tài khoản <strong>" + Encode(UserName) + "</strong>";
        }

        /// <param name="param">attached items, "TemplateID,Count,ValidDays,Strengthen,Attack,Defend,Agility,Luck,IsBind|..."</param>
        public static string SendMailByAdmin(string title, string content, string UserName, string param)
        {
            if (string.IsNullOrEmpty(content) || content.Length > 2000)
            {
                content = "You got Items from Administrators! How Luck!";
            }
            if (string.IsNullOrEmpty(title) || title.Length > 1000)
            {
                title = "Email from Administrators!";
            }
            try
            {
                using PlayerBussiness business = new PlayerBussiness();
                PlayerInfo player = FindPlayer(business, UserName);
                if (player == null)
                {
                    return "Bạn chưa tạo nhân vật.";
                }
                if (string.IsNullOrEmpty(param))
                {
                    MailInfo mail = new MailInfo
                    {
                        Title = title,
                        Content = content,
                        ReceiverID = player.ID,
                        Receiver = player.NickName,
                        Sender = "Administrators",
                        SenderID = 0
                    };
                    if (!business.SendMail(mail))
                    {
                        return "Gửi thất bại!";
                    }
                    log.InfoFormat("SendMail to {0} ({1}): {2}", player.NickName, player.ID, title);
                    return "Gởi thư cho Tài khoản(Nickname) <strong>" + Encode(UserName) + "</strong>, thành công!";
                }

                string result = "Gửi thất bại!";
                int maxCompose = Setting("MAX_COMPOSE", 50);
                int maxStrengthen = Setting("MAX_STRENGTGTHEN", 15);
                List<ItemInfo> items = new List<ItemInfo>();
                foreach (string itemStr in param.Split('|'))
                {
                    string[] f = itemStr.Split(',');
                    if (f.Length < 9 || f[0] == "")
                    {
                        continue;
                    }
                    ItemTemplateInfo template = ItemMgr.FindItemTemplate(int.Parse(f[0]));
                    if (template == null)
                    {
                        result = "Template not found!";
                        continue;
                    }
                    ItemInfo item = ItemInfo.CreateFromTemplate(template, 1, 105);
                    item.Count = int.Parse(f[1]);
                    item.ValidDate = int.Parse(f[2]);
                    item.StrengthenLevel = Math.Min(int.Parse(f[3]), maxStrengthen);
                    item.AttackCompose = Math.Min(int.Parse(f[4]), maxCompose);
                    item.DefendCompose = Math.Min(int.Parse(f[5]), maxCompose);
                    item.AgilityCompose = Math.Min(int.Parse(f[6]), maxCompose);
                    item.LuckCompose = Math.Min(int.Parse(f[7]), maxCompose);
                    item.IsBinds = Convert.ToBoolean(f[8]);
                    items.Add(item);
                }
                if (items.Count > 0)
                {
                    WorldEventMgr.SendItemsToMail(items, player.ID, player.NickName, title, content);
                    log.InfoFormat("SendMail with items to {0} ({1}): {2}", player.NickName, player.ID, param);
                    result = "Gởi thư và Items đính kèm cho <strong>" + Encode(player.NickName) + "</strong>, thành công!";
                }
                return result;
            }
            catch (Exception e)
            {
                log.Error("SendMailByAdmin", e);
                return Encode(e.ToString());
            }
        }

        public static string Load_ItemIcon(int CategoryID, int page_number, bool show_smartpaginator)
        {
            using ProduceBussiness business = new ProduceBussiness();
            ItemTemplateInfo[] items = business.GetSingleCategory(CategoryID);
            string html = "<div style=\"font-size: 18px; font-weight: bold; color: #003366; margin: 3px 3px 3px 3px; height: 25px;\">List Item</div><div style=\"min-height: 407px; width: 100%\"><table>"
                + IconGrid(items, page_number, 16, 4, item => item.TemplateID + ", " + item.MaxCount)
                + "</table></div><span style=\"font-size:12px; color:#FF0000\">Tips: Click vào Items bạn cần, chương trình sẻ tự động thêm Item vào Item đính kèm.</span>";
            if (show_smartpaginator)
            {
                html += Paginator(items.Length, 4, 16, "loading_ItemIcon(" + CategoryID + ", newPage, false);");
            }
            return html;
        }

        public static string Load_ItemIcon2(int Slot, int page_number, bool show_smartpaginator)
        {
            using ProduceBussiness business = new ProduceBussiness();
            ItemTemplateInfo[] items = business.GetFusionType();
            string html = "<table>"
                + IconGrid(items, page_number, 20, 5, item => item.TemplateID + "," + Slot)
                + "</table></div><span style=\"font-size:12px; color:#FF0000\">Tips: Click vào Items bạn cần, chương trình sẻ tự động thêm Item vào slot.</span>";
            if (show_smartpaginator)
            {
                html += Paginator(items.Length, 3, 20, "loading_ItemIcon2(" + Slot + ", newPage, false);");
            }
            return html;
        }

        /// <param name="Keys">the slot options, "validDays.strengthen.attack.defend.agility.luck"; "default" for a newly
        /// attached item: permanent, and composed to MAX_COMPOSE when the item can be composed. The page reads the
        /// options back from the returned image's alt text into the slot.</param>
        public static string AddItemsMail(int GoodIDs, string Keys, int isPopup)
        {
            using ProduceBussiness business = new ProduceBussiness();
            ItemTemplateInfo item = business.GetSingleGoods(GoodIDs);
            if (item == null)
            {
                return "";
            }
            if (Keys == "default")
            {
                int compose = item.CanCompose ? Setting("MAX_COMPOSE", 50) : 0;
                Keys = "0.0." + compose + "." + compose + "." + compose + "." + compose;
            }
            string popup = (item.CanCompose || item.CanStrengthen) && isPopup == 1
                ? item.TemplateID + ",ItemsInfo." + Encode(Keys) + ",popupInfo"
                : item.TemplateID + ",ItemsInfo,popup";
            return ItemHtml.Icon(ItemHtml.IconUrl(item), "class=\"personPopupTrigger\" alt=\"" + popup + "\"");
        }

        /// <summary>The user name, else the nickname, else the character ID.</summary>
        private static PlayerInfo FindPlayer(PlayerBussiness business, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }
            PlayerInfo player = business.GetUserSingleByUserName(name) ?? business.GetUserSingleByNickName(name);
            if (player == null && int.TryParse(name, out int id))
            {
                player = business.GetUserSingleByUserID(id);
            }
            return player;
        }

        /// <param name="clickArgs">arguments of the call_addItems(...) click handler for an item</param>
        private static string IconGrid(ItemTemplateInfo[] items, int page, int perPage, int perRow, Func<ItemTemplateInfo, string> clickArgs)
        {
            int i = Math.Max(perPage * page - perPage, 0);
            int end = Math.Min(perPage * page, items.Length);
            string html = "";
            do
            {
                html += "<tr>";
                for (int s = 0; s < perRow && i < end; s++, i++)
                {
                    ItemTemplateInfo item = items[i];
                    int id = item.TemplateID;
                    html += "<td style=\"text-align: center; border: thin #000080 solid; width: 95px; height: 95px;\"";
                    if (item.Pic != "default" && id != -300 && id != -200 && id != -100 && id != 10200)
                    {
                        html += "onmouseout=\"$(this).css({ background: '#C4C4C4'});\"";
                        html += "onmouseover=\"$(this).css({ background: '#D2FFA6'});\">";
                        html += ItemHtml.IconBox(ItemHtml.Icon(ItemHtml.IconUrl(item), "class=\"personPopupTrigger\" onclick=\"call_addItems("
                            + clickArgs(item) + ");\" alt=\"" + id + ",ItemsInfo,popup\"", "cursor: pointer; "));
                    }
                    else
                    {
                        html += ">" + ItemHtml.IconBox(ItemHtml.Icon(ItemHtml.IconUrl(item), "alt=\"\""));
                    }
                    html += "</td>";
                }
                html += "</tr>";
            } while (i < end);
            return html;
        }

        private static string Paginator(int total, int length, int perPage, string onChange)
        {
            return "<script type=\"text/javascript\">$(function() {$('#show_smartpaginator').smartpaginator({ totalrecords: " + total
                + ",length: " + length + ", recordsperpage: " + perPage + ", controlClass:'pager2', onchange: function (newPage) {"
                + onChange + "}});});</script>";
        }

        /// <summary>The Server_Config options the "Cấu hình" page (Admin/serverConfig.aspx) can switch on and off.</summary>
        private static readonly string[] GameOptions =
        {
            nameof(GameProperties.CardResetAlwaysMax),
            nameof(GameProperties.TexpNoDailyLimit),
            nameof(GameProperties.ItemAdvanceAlwaysSucceed),
            nameof(GameProperties.WishBeadAlwaysSucceed),
            nameof(GameProperties.StrengthenAlwaysSucceed),
            nameof(GameProperties.FreeMatchCountsAsGuildWar),
            nameof(GameProperties.SkipCheckCode),
            nameof(GameProperties.BotMatchIgnoreAimTime),
        };

        /// <summary>The current value of each game option; an option missing from Server_Config has its GameProperties default.</summary>
        public static Dictionary<string, bool> GetGameOptions()
        {
            Dictionary<string, bool> options = new Dictionary<string, bool>();
            using ServiceBussiness business = new ServiceBussiness();
            foreach (string name in GameOptions)
            {
                ServerProperty property = business.GetServerPropertyByKey(name);
                options[name] = property != null && bool.TryParse(property.Value, out bool on) ? on : DefaultOf(name);
            }
            return options;
        }

        private static bool DefaultOf(string name)
        {
            ConfigPropertyAttribute attribute = typeof(GameProperties).GetField(name)?.GetCustomAttribute<ConfigPropertyAttribute>();
            return attribute?.DefaultValue is bool value && value;
        }

        /// <summary>
        /// Saves one game option, then asks the center server to make every server re-read Server_Config.
        /// Saved is false for an unknown option or a database error; Reloaded is false when the center
        /// server could not be reached (the value then applies at the next game server start).
        /// </summary>
        public static object SetGameOption(string Name, bool Value)
        {
            if (Array.IndexOf(GameOptions, Name) < 0)
            {
                return new { Saved = false, Reloaded = false };
            }
            bool saved;
            using (ServiceBussiness business = new ServiceBussiness())
            {
                saved = business.UpdateServerPropertyByKey(Name, Value.ToString());
            }
            bool reloaded = false;
            if (saved)
            {
                log.InfoFormat("Game option {0} = {1}", Name, Value);
                try
                {
                    using CenterServiceClient center = new CenterServiceClient();
                    reloaded = center.Reload(eReloadType.gameproperties.ToString());
                }
                catch (Exception e)
                {
                    log.Warn("Reloading the game options on the servers failed", e);
                }
            }
            return new { Saved = saved, Reloaded = reloaded };
        }

        /// <summary>The server notices (Edictum_List) for the "Thông báo" page (Admin/notice.aspx).</summary>
        public static object GetEdictums()
        {
            using ProduceBussiness business = new ProduceBussiness();
            List<object> list = new List<object>();
            foreach (EdictumInfo info in business.GetAllEdictum())
            {
                list.Add(new
                {
                    info.ID,
                    info.Title,
                    Text = (info.Text ?? "").Replace("\r\n", "\n"),
                    BeginDate = info.BeginDate.ToString("yyyy-MM-dd"),
                    EndDate = info.EndDate.ToString("yyyy-MM-dd"),
                });
            }
            return list;
        }

        /// <summary>
        /// Adds (ID 0) or updates a server notice, then asks the game servers to reload the notice list.
        /// Players see a notice from BeginDate until the day before EndDate, when they log in.
        /// </summary>
        public static object SaveEdictum(int ID, string Title, string Text, string BeginDate, string EndDate)
        {
            if (string.IsNullOrWhiteSpace(Title)
                || !DateTime.TryParse(BeginDate, out DateTime begin) || !DateTime.TryParse(EndDate, out DateTime end))
            {
                return new { Saved = false, Reloaded = false };
            }
            EdictumInfo info = new EdictumInfo
            {
                ID = ID,
                Title = Title.Trim(),
                // only \n: the Flash client breaks the line at \r and again at \n, so \r\n shows a blank line
                Text = (Text ?? "").Replace("\r\n", "\n").Replace("\r", "\n"),
                BeginDate = begin.Date,
                EndDate = end.Date,
            };
            bool saved;
            using (ProduceBussiness business = new ProduceBussiness())
            {
                saved = business.SaveEdictum(info);
            }
            if (saved)
            {
                log.InfoFormat("Server notice {0} saved: {1}", ID == 0 ? "(new)" : ID.ToString(), info.Title);
            }
            return new { Saved = saved, Reloaded = saved && ReloadEdictums() };
        }

        public static object DeleteEdictum(int ID)
        {
            bool deleted;
            using (ProduceBussiness business = new ProduceBussiness())
            {
                deleted = business.DeleteEdictum(ID);
            }
            if (deleted)
            {
                log.InfoFormat("Server notice {0} deleted", ID);
            }
            return new { Saved = deleted, Reloaded = deleted && ReloadEdictums() };
        }

        /// <summary>The game servers keep the list of notices (not their text): false when the center server is not reachable.</summary>
        private static bool ReloadEdictums()
        {
            try
            {
                using CenterServiceClient center = new CenterServiceClient();
                return center.Reload(eReloadType.edictum.ToString());
            }
            catch (Exception e)
            {
                log.Warn("Reloading the server notices on the game servers failed", e);
                return false;
            }
        }

        private static string NotBuiltYet()
        {
            return "<script type=\"text/javascript\">tb_showMiniPanel();</script>";
        }
    }
}
