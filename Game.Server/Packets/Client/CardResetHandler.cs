using Bussiness;
using Game.Base.Packets;
using Game.Server.GameUtils;
using Game.Server.Managers;
using SqlDataProvider.Data;
using System.Collections.Generic;

namespace Game.Server.Packets.Client
{
    [PacketHandler(196, "卡牌洗点")]
    public class CardResetHandler : IPacketHandler
    {
        // The highest value NextSmallValue(5, NextSmallValue(6, 50)) can give: the inner roll is at most 44, the outer at most 44 - 5.
        private const int MaxResetRoll = 39;

        public int HandlePacket(GameClient client, GSPacketIn packet)
        {
            int num = packet.ReadInt();
            int num2 = packet.ReadInt();
            RandomSafe randomSafe = new RandomSafe();
            CardInventory cardBag = client.Player.CardBag;
            List<UsersCardInfo> infos;
            using (PlayerBussiness playerBussiness = new PlayerBussiness())
            {
                infos = playerBussiness.GetUserCardEuqip(client.Player.PlayerCharacter.ID);
            }
            if (num2 < 5)
            {
                return 0;
            }
            UsersCardInfo itemAt = cardBag.GetItemAt(num2);
            if (itemAt == null)
            {
                return 0;
            }
            if (num == 0)
            {
                CardUpdateConditionInfo cardUpdateCondition = CardMgr.GetCardUpdateCondition(itemAt.Level);
                if (cardUpdateCondition != null && cardUpdateCondition.ResetCardCount > 0)
                {
                    bool flag = false;
                    if (itemAt.Count >= cardUpdateCondition.ResetCardCount)
                    {
                        flag = true;
                        itemAt.Count -= cardUpdateCondition.ResetCardCount;
                        cardBag.UpdateCard(itemAt);
                    }
                    else if (client.Player.RemoveMoney(cardUpdateCondition.ResetMoney) > 0)
                    {
                        flag = true;
                    }
                    if (flag)
                    {
                        int[] array = new int[4];
                        for (int i = 0; i < array.Length; i++)
                        {
                            array[i] = GameProperties.CardResetAlwaysMax ? MaxResetRoll : randomSafe.NextSmallValue(5, randomSafe.NextSmallValue(6, 50));
                        }
                        if (client.Player.CardResetTempProp.ContainsKey(itemAt.TemplateID))
                        {
                            client.Player.CardResetTempProp[itemAt.TemplateID] = array;
                        }
                        else
                        {
                            client.Player.CardResetTempProp.Add(itemAt.TemplateID, array);
                        }
                        GSPacketIn gSPacketIn = new GSPacketIn(196);
                        gSPacketIn.WriteInt(4);
                        gSPacketIn.WriteInt(itemAt.Attack + array[0]);
                        gSPacketIn.WriteInt(itemAt.Defence + array[1]);
                        gSPacketIn.WriteInt(itemAt.Agility + array[2]);
                        gSPacketIn.WriteInt(itemAt.Luck + array[3]);
                        client.SendTCP(gSPacketIn);
                    }
                    else
                    {
                        client.Player.SendMessage(LanguageMgr.GetTranslation("GameServer.CardReset.Msg2"));
                    }
                }
                else
                {
                    client.Player.SendMessage(LanguageMgr.GetTranslation("GameServer.CardReset.Msg3"));
                }
            }
            else if (client.Player.CardResetTempProp.ContainsKey(itemAt.TemplateID))
            {
                int[] array2 = client.Player.CardResetTempProp[itemAt.TemplateID];
                itemAt.AttackReset = array2[0];
                itemAt.DefenceReset = array2[1];
                itemAt.AgilityReset = array2[2];
                itemAt.LuckReset = array2[3];
                UsersCardInfo cardEquip = cardBag.GetCardEquip(itemAt.TemplateID);
                if (cardEquip != null)
                {
                    cardEquip.CopyProp(itemAt);
                    cardBag.UpdateCard(cardEquip);
                    client.Player.EquipBag.UpdatePlayerProperties();
                }
                //client.Player.OnCardEquipEvent(infos);
                cardBag.UpdateCard(itemAt);
                client.Player.CardResetTempProp.Remove(itemAt.TemplateID);
                client.Player.SendMessage(LanguageMgr.GetTranslation("GameServer.CardReset.Msg1"));
            }
            return 0;
        }
    }
}
