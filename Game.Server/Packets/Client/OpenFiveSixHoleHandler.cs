using Bussiness;
using Game.Base.Packets;
using Game.Server.Managers;
using SqlDataProvider.Data;
using System;

namespace Game.Server.Packets.Client
{
    [PacketHandler(217, "开孔")]
    public class OpenFiveSixHoleHandler : IPacketHandler
    {
        public static Random random = new Random();

        public int HandlePacket(GameClient client, GSPacketIn packet)
        {
            int slot = packet.ReadInt();
            int num = packet.ReadInt();
            int templateId = packet.ReadInt();
            if (DateTime.Compare(client.Player.LastOpenHole.AddMilliseconds(100.0), DateTime.Now) > 0)
            {
                client.Out.SendMessage(eMessageType.GM_NOTICE, LanguageMgr.GetTranslation("GameServer.OpenHole.TooQuickly"));
                return 0;
            }
            client.Player.LastOpenHole = DateTime.Now;
            ItemInfo itemAt = client.Player.StoreBag.GetItemAt(slot);
            if (itemAt != null && (itemAt.Template.CategoryID == 7 || itemAt.Template.CategoryID == 1 || itemAt.Template.CategoryID == 5))
            {
                ItemInfo itemByTemplateID = client.Player.PropBag.GetItemByTemplateID(0, templateId);
                bool isBinds = itemAt.IsBinds;
                if (itemByTemplateID == null || itemByTemplateID.Count <= 0)
                {
                    return 0;
                }
                if ((itemAt != null && itemAt.IsBinds) || (itemByTemplateID?.IsBinds ?? false))
                {
                    isBinds = true;
                }
                if (itemByTemplateID.IsBinds && !itemAt.IsBinds)
                {
                    client.Player.StoreBag.UpdateItem(itemAt);
                }
                bool val = false;
                if (num == 5 || num == 6)
                {
                    int level = num == 5 ? itemAt.Hole5Level : itemAt.Hole6Level;
                    if (itemByTemplateID.isDrill(level))
                    {
                        // Use every matching drill at once. A drill only fits one hole level,
                        // so this stops at the first level-up or when the drills run out.
                        string[] expList = GameProperties.HoleLevelUpExpList.Split('|');
                        int exp = num == 5 ? itemAt.Hole5Exp : itemAt.Hole6Exp;
                        client.Player.PropBag.BeginChanges();
                        try
                        {
                            while (itemByTemplateID != null && level < expList.Length)
                            {
                                bool lastOfStack = itemByTemplateID.Count <= 1;
                                if (!client.Player.PropBag.RemoveCountFromStack(itemByTemplateID, 1))
                                {
                                    break;
                                }
                                exp += random.Next(itemByTemplateID.Template.Property7, itemByTemplateID.Template.Property8);
                                if (exp >= int.Parse(expList[level]))
                                {
                                    level++;
                                    exp = 0;
                                    val = true;
                                    break;
                                }
                                if (lastOfStack)
                                {
                                    // That stack is used up: carry on with the next stack of the same drill, if any.
                                    itemByTemplateID = client.Player.PropBag.GetItemByTemplateID(0, templateId);
                                }
                            }
                        }
                        finally
                        {
                            client.Player.PropBag.CommitChanges();
                        }
                        if (num == 5)
                        {
                            itemAt.Hole5Level = level;
                            itemAt.Hole5Exp = exp;
                            if (level > 0 && itemAt.Hole5 < 0)
                            {
                                itemAt.Hole5 = 0;
                            }
                        }
                        else
                        {
                            itemAt.Hole6Level = level;
                            itemAt.Hole6Exp = exp;
                            if (level > 0 && itemAt.Hole6 < 0)
                            {
                                itemAt.Hole6 = 0;
                            }
                        }
                    }
                    else
                    {
                        client.Player.SendMessage(LanguageMgr.GetTranslation("OpenFiveSixHoleHandler.WrongDrill"));
                    }
                }
                else
                {
                    Console.WriteLine("不存在宝珠孔： " + num);
                }
                client.Player.StoreBag.UpdateItem(itemAt);
                GSPacketIn gSPacketIn = new GSPacketIn(217);
                gSPacketIn.WriteByte(0);
                gSPacketIn.WriteBoolean(val);
                gSPacketIn.WriteInt(num);
                client.Player.SendTCP(gSPacketIn);
            }
            else
            {
                client.Player.SendMessage(LanguageMgr.GetTranslation("OpenFiveSixHoleHandler.CannotOpen"));
            }
            return 0;
        }
    }
}
