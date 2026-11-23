using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml;
using Bussiness;
using SqlDataProvider.Data;

namespace GameAdmin.Web
{
    /// <summary>
    /// Code-behind of the GameAdmin pages, ported: each method returns the text written before the page
    /// (WebForms' Response.Write) and fills the page's asp:Label texts.
    /// </summary>
    public sealed class ItemPages
    {
        private readonly string root;

        public ItemPages(string root)
        {
            this.root = root;
        }

        /// <summary>Admin/sendMail5Item.aspx: the item category menu and the hidden fields of the 5 attachment slots.</summary>
        public void SendMail5Item(Dictionary<string, string> labels)
        {
            string html = Menu("Admin/data/MiniMenuItems.xml");
            for (int s = 1; s < 6; s++)
            {
                html += "<input id=\"HiddenItem" + s + "\" value=\"\" type=\"hidden\" />";
                html += "<input id=\"HiddenCount" + s + "\" value=\"1\" type=\"hidden\" />";
                foreach (string field in new[] { "Valid", "Streng", "Attack", "Defend", "Agility", "Luck" })
                {
                    html += "<input id=\"Hidden" + field + s + "\" value=\"0\" type=\"hidden\" />";
                }
            }
            labels["ShowLoadMenu"] = html;
        }

        /// <summary>Admin/popup.aspx?IDs=TemplateID&amp;keys=ItemsInfo: the item tooltip.</summary>
        public string Popup(string ids, string keys, Dictionary<string, string> labels)
        {
            if (string.IsNullOrEmpty(ids) || string.IsNullOrEmpty(keys) || !int.TryParse(ids, out int templateID))
            {
                return "Request Fail!";
            }
            if (keys != "ItemsInfo")
            {
                return WebUtility.HtmlEncode(keys);
            }
            return ItemInfo(templateID, null, labels);
        }

        /// <summary>
        /// Admin/popupInfo.aspx?IDs=TemplateID&amp;keys=ItemsInfo.valid.strengthen.attack.defend.agility.luck:
        /// the tooltip of an attached item with its options.
        /// </summary>
        public string PopupInfo(string ids, string keys, Dictionary<string, string> labels)
        {
            if (string.IsNullOrEmpty(ids) || string.IsNullOrEmpty(keys) || !int.TryParse(ids, out int templateID))
            {
                return "Request Fail!";
            }
            string[] parts = keys.Split('.');
            if (parts[0] != "ItemsInfo")
            {
                return WebUtility.HtmlEncode(keys);
            }
            int[] options = new int[6];
            for (int i = 0; i < options.Length; i++)
            {
                if (parts.Length <= i + 1 || !int.TryParse(parts[i + 1], out options[i]))
                {
                    return "Request Fail!";
                }
            }
            return ItemInfo(templateID, options, labels);
        }

        /// <summary>Admin/customItem.aspx?IDs=TemplateID&amp;keys=customItems,slot: the attachment options form.</summary>
        public string CustomItem(string ids, string keys, Dictionary<string, string> labels)
        {
            if (string.IsNullOrEmpty(ids) || string.IsNullOrEmpty(keys) || !int.TryParse(ids, out int templateID))
            {
                return "Request Fail!";
            }
            string[] parts = keys.Split(',');
            if (parts[0] != "customItems" || parts.Length < 2 || !int.TryParse(parts[1], out int slot))
            {
                return WebUtility.HtmlEncode(keys);
            }
            ItemTemplateInfo item = GetItem(templateID);
            if (item == null)
            {
                return "Không tìm thấy Item có ID: " + templateID;
            }
            string ok = "<p style=\"width: 100%; height:18px; text-align:center;\"><a style=\"font-size: 15px; font-weight: bold;\" href=\"javascript:void(0);\" onclick=\"set_customitems("
                + slot + "," + item.TemplateID + ");\">[Xác nhận]</a>&nbsp;&nbsp;&nbsp;<a style=\"font-size: 15px; font-weight: bold;\" href=\"javascript:void(0);\" onclick=\"hide_customitem();\">[Huỷ]</a></p>";
            const string hideStrengthen = "<script type=\"text/javascript\">$(\"#sp_lbStr\").css({ display: \"none\" }); $(\"#sp_SelectStr\").css({ display: \"none\" })</script>";
            const string hideCompose = "<script type=\"text/javascript\">$(\"#sp_lbComp\").css({ display: \"none\" }); $(\"#sp_SelectComp\").css({ display: \"none\" })</script>";
            if (item.CanCompose || item.CanStrengthen || item.CanEquip)
            {
                labels["Btn"] = ok + (item.CanStrengthen ? "" : hideStrengthen) + (item.CanCompose ? "" : hideCompose);
                return "";
            }
            return "Vật phẩm này không có tuỳ chọn!<p style=\"width: 100%; height:18px; text-align:center;\"><a style=\"font-size: 15px; font-weight: bold;\" href=\"javascript:void(0);\" onclick=\"hide_customitem();\">[Đóng]</a></p>"
                + "<script type=\"text/javascript\">$(\"#Div_main\").css({ display: \"none\" })</script>";
        }

        /// <param name="options">null for popup.aspx; for popupInfo.aspx: valid days, strengthen, attack, defend, agility, luck</param>
        private string ItemInfo(int templateID, int[] options, Dictionary<string, string> labels)
        {
            ItemTemplateInfo item = GetItem(templateID);
            if (item == null)
            {
                return "Không tìm thấy Item có ID: " + templateID;
            }
            bool withOptions = options != null;
            string name = item.Name == "" ? "Default Item" : item.Name;
            (string quality, string color) = item.Quality switch
            {
                1 => ("Thô", "808080"),
                2 => ("Thường", "006600"),
                3 => ("Ưu", "0000FF"),
                4 => ("Tinh Anh", "990099"),
                5 => (withOptions ? "Xuất sắc" : "xuất sắc", "FF9900"),
                _ => ("", "")
            };
            int strengthen = withOptions ? options[1] : 0;

            labels["ShowTitles"] = "<span style=\"color:#" + color + "\"> " + name
                + (withOptions && item.CanStrengthen ? " (+" + strengthen + ")" : "") + "</span>";

            string mainStat = item.CategoryID switch
            {
                1 or 5 => "Áo Giáp",
                7 or 27 => "Sát thương",
                17 or 31 => item.TemplateID == 17003 || item.TemplateID == 17004 ? "Áo Giáp" : "Hồi phục",
                _ => ""
            };
            if (mainStat != "")
            {
                labels["ShowTitles0"] = mainStat + ":&nbsp;&nbsp;<span class=\"span_style205\"> " + item.Property7
                    + (withOptions ? " (+" + ItemHtml.StrengthenBonus(item.Property7, strengthen) + ")" : "") + "</span>";
            }
            if (item.Attack != 0)
            {
                (string label, int value)[] stats =
                {
                    ("ShowAttack", item.Attack), ("ShowDefence", item.Defence), ("ShowAgility", item.Agility), ("ShowLuck", item.Luck)
                };
                string[] names = { "Tấn Công", "Phòng thủ", "Nhanh nhẹn", "May mắn" };
                for (int i = 0; i < stats.Length; i++)
                {
                    labels[stats[i].label] = withOptions
                        ? names[i] + ":&nbsp;&nbsp; " + stats[i].value + " (+" + options[i + 2] + ")"
                        : names[i] + ":&nbsp;&nbsp;<span class=\"span_style205\"> " + stats[i].value + "</span>";
                }
            }
            labels["ShowDescription"] = item.Description;
            labels["ShowCategoryID"] = "Category:&nbsp;&nbsp;" + item.CategoryID;
            labels["ShowTemplateID"] = "ID:&nbsp;&nbsp;" + item.TemplateID;
            labels["Type"] = "Loại:&nbsp;&nbsp;<span class=\"style202\"> " + CategoryName(item.CategoryID) + "</span>";
            labels["Quality"] = "Phẩm chất:&nbsp;&nbsp;<span style=\"color:#" + color + "\"> " + quality + "</span>";
            if (item.NeedSex == 1)
            {
                labels["LbSex"] = withOptions
                    ? "<span style=\"color:#FF0000\">Gới tính:&nbsp;&nbsp;Nam</span>"
                    : "Gới tính:&nbsp;&nbsp;<span style=\"color:#FF0000\">Nam</span>";
            }
            else if (item.NeedSex == 2)
            {
                labels["LbSex"] = "Gới tính:&nbsp;&nbsp;<span style=\"color:#006600\">Nữ</span>";
            }
            if (item.CanStrengthen)
            {
                labels["LbCan"] = "Có thể cường hoá";
            }
            else if (item.CanCompose)
            {
                labels["LbCan"] = "Có thể hợp thành";
            }
            if (withOptions)
            {
                if (strengthen != 0)
                {
                    labels["Lb_str"] = "<img alt=\"\" src=\"../Images/gunny/strengthenlevel/strengthenlevel" + strengthen + ".png\" />";
                }
                labels["Lb_valid"] = options[0] != 0
                    ? "Còn lại " + options[0] + " Ngày"
                    : "<span style=\"color:#FFCC00\">Hiệu Lực vĩnh cửu</span>";
            }
            return "";
        }

        private static ItemTemplateInfo GetItem(int templateID)
        {
            using ProduceBussiness business = new ProduceBussiness();
            return business.GetSingleGoods(templateID);
        }

        /// <summary>The category name from Admin/data/LoadItemsCategory.xml.</summary>
        private string CategoryName(int categoryID)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(Path.Combine(root, "Admin/data/LoadItemsCategory.xml"));
            foreach (XmlElement node in doc.SelectNodes("Result/Item"))
            {
                if (node.GetAttribute("ID") == categoryID.ToString())
                {
                    return node.GetAttribute("Name");
                }
            }
            return "";
        }

        /// <summary>Default.aspx.cs GetXML: a menu of &lt;List Name Script/&gt; entries.</summary>
        private string Menu(string file)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(Path.Combine(root, file));
            string html = "<div class=\"menu\" id=\"NavigationMenu2\"><ul class=\"level1\">";
            foreach (XmlElement node in doc.DocumentElement.ChildNodes.OfType<XmlElement>())
            {
                string name = node.GetAttribute("Name");
                string script = node.GetAttribute("Script");
                html += "<li><a  href=\"javascript:void(0);\" class=\"level1\" id=\"menuloaddata\" onclick=\"" + (script == "" ? "NULL" : script)
                    + ";\">" + (name == "" ? "NULL" : name) + "</a></li>";
            }
            return html + "</ul></div>";
        }
    }
}
