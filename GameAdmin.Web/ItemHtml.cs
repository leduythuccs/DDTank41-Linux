using System;
using System.Collections.Generic;
using System.Configuration;
using SqlDataProvider.Data;

namespace GameAdmin.Web
{
    /// <summary>GameAdmin/adminClass.cs: item icon URLs and the search result table.</summary>
    public static class ItemHtml
    {
        private const string NoIcon = "../Images/7road.png";

        /// <summary>Icon path under the resource site by CategoryID; {0} = Pic, {1} = "m"/"f".</summary>
        private static readonly Dictionary<int, string> IconPaths = new Dictionary<int, string>
        {
            [1] = "image/equip/{1}/head/{0}/icon_1.png",
            [2] = "image/equip/{1}/glass/{0}/icon_1.png",
            [3] = "image/equip/{1}/hair/{0}/icon_1.png",
            [4] = "image/equip/{1}/eff/{0}/icon_1.png",
            [5] = "image/equip/{1}/cloth/{0}/icon_1.png",
            [6] = "image/equip/{1}/face/{0}/icon_1.png",
            [7] = "image/arm/{0}/00.png",
            [27] = "image/arm/{0}/00.png",
            [8] = "image/equip/armlet/{0}/icon.png",
            [9] = "image/equip/ring/{0}/icon.png",
            [11] = "image/unfrightprop/{0}/icon.png",
            [30] = "image/unfrightprop/{0}/icon.png",
            [34] = "image/unfrightprop/{0}/icon.png",
            [35] = "image/unfrightprop/{0}/icon.png",
            [36] = "image/unfrightprop/{0}/icon.png",
            [40] = "image/unfrightprop/{0}/icon.png",
            [60] = "image/unfrightprop/{0}/icon.png",
            [12] = "image/task/{0}/icon.png",
            [13] = "image/equip/{1}/suits/{0}/icon_1.png",
            [14] = "image/equip/necklace/{0}/icon.png",
            [15] = "image/equip/wing/{0}/icon.png",
            [16] = "image/specialprop/chatBall/{0}/icon.png",
            [17] = "image/equip/offhand/{0}/icon.png",
            [31] = "image/equip/offhand/{0}/icon.png",
            [18] = "image/cardbox/{0}/icon.png",
            [19] = "image/prop/{0}/icon.png",
            [20] = "image/prop/{0}/icon.png",
            [23] = "image/prop/{0}/icon.png",
            [28] = "image/prop/{0}/icon.png",
            [29] = "image/prop/{0}/icon.png",
            [25] = "image/gift/{0}/icon.png",
            [26] = "image/card/{0}/icon.jpg",
            [50] = "image/petequip/arm/{0}/icon.png",
            [51] = "image/petequip/hat/{0}/icon.png",
            [52] = "image/petequip/cloth/{0}/icon.png",
        };

        public static string IconUrl(ItemTemplateInfo item)
        {
            if (item == null || item.Pic == "default" || !IconPaths.TryGetValue(item.CategoryID, out string path))
            {
                return NoIcon;
            }
            return ConfigurationManager.AppSettings["Resource"] + string.Format(path, item.Pic, item.NeedSex == 1 ? "m" : "f");
        }

        /// <summary>Bonus of a strengthened item: base * 1.1^level - base.</summary>
        public static double StrengthenBonus(int value, int level)
        {
            return Math.Round(value * Math.Pow(1.1, level) - value);
        }

        public static string MidTable(string name, int categoryID, int templateID, string icon, string pic)
        {
            string html = "<td class=\"style7\"><table class=\"style1\"><tr><td class=\"style6\" colspan=\"2\">"
                + (name == "" ? "Default" : name)
                + "</td></tr><tr><td class=\"style2\" rowspan=\"3\">"
                + "<img class=\"personPopupTrigger\" alt=\"" + templateID + ",ItemsInfo,popup\" "
                + (categoryID == 26 ? " style=\"height: 90px;\" " : "") + "src=\"" + icon + "\" />";
            bool disabled = categoryID == 21 || categoryID == 31 || categoryID == 10 || categoryID == 27 || pic == "default";
            string[] actions = { "Thêm vào...", "Xoá Item", "Chỉnh sửa" };
            for (int i = 0; i < actions.Length; i++)
            {
                html += (i == 0 ? "</td>" : "</td></tr><tr>") + "<td class=\"style6a\">";
                html += disabled
                    ? actions[i]
                    : "<a href=\"javascript:void(0);\" onclick=\"call_CSharp(" + (i + 1) + "," + templateID + ");\" >" + actions[i] + "</a>";
            }
            return html + "</td></tr></table></td>";
        }
    }
}
