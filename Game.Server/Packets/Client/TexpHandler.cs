using Bussiness;
using Game.Base.Packets;
using Game.Logic;
using Game.Server.Buffer;
using Game.Server.Managers;
using SqlDataProvider.Data;
using System;

namespace Game.Server.Packets.Client
{
    [PacketHandler(99, "场景用户离开")]
    public class TexpHandler : IPacketHandler
    {
        public int HandlePacket(GameClient client, GSPacketIn packet)
        {
            int selectIndex = packet.ReadInt();
            int templateID = packet.ReadInt();
            int m_place = packet.ReadInt();
            string text = "HP";
            ItemInfo item = client.Player.StoreBag.GetItemAt(m_place);
            TexpInfo info = client.Player.PlayerCharacter.Texp;
            int oldExp = 0;
            if (item == null || info == null || item.TemplateID != templateID || client.Player.isPlayerWarrior())
            {
                return 0;
            }
            if (!item.isTexp())
            {
                return 0;
            }
            int limitCount = client.Player.PlayerCharacter.Grade;
            if (client.Player.PlayerCharacter.VIPLevel <= 2)
            {
                limitCount = limitCount * 2;
            }
            else
            {
                limitCount = limitCount * 3;
            }
            if (client.Player.UsePayBuff(BuffType.Train_Good))
            {
                AbstractBuffer ofType = client.Player.BufferList.GetOfType(BuffType.Train_Good);
                limitCount += ofType.Info.Value;
            }
            if (info.texpTaskDate.Date.AddDays(1.0).Date <= DateTime.Now.Date && info.texpCount >= limitCount)
            {
                info.texpCount = 0;
                info.texpTaskDate = DateTime.Now;
            }
            if (info.texpCount >= limitCount)
            {
                client.Out.SendMessage(eMessageType.GM_NOTICE, LanguageMgr.GetTranslation("texpSystem.texpCountToplimit"));
            }
            else
            {
                // One click uses the whole stack in the slot, as far as the daily limit allows and until
                // the stat reaches its cap (character level + 5, as the client checks).
                int perPotion = item.Template.Property2;
                int exp = ExerciseMgr.getExp(selectIndex, info);
                int capLevel = Math.Min(client.Player.PlayerCharacter.Grade + 5, ExerciseMgr.GetMaxLevel());
                int capExp = ExerciseMgr.FindExercise(capLevel)?.GP ?? int.MaxValue;
                int used = 0;
                while (used < item.Count && info.texpCount + used < limitCount && exp + used * perPotion < capExp)
                {
                    used++;
                }
                if (used == 0)
                {
                    client.Out.SendMessage(eMessageType.GM_NOTICE, "Cấp tu luyện này đã đạt mức cao nhất!");
                    return 0;
                }
                client.Player.OnUsingItem(templateID, used);
                client.Player.StoreBag.RemoveCountFromStack(item, used);
                int gain = perPotion * used;
                switch (selectIndex)
                {
                    case 0:
                        oldExp = info.hpTexpExp;
                        info.hpTexpExp += gain;
                        client.Player.OnUsingItem(45005, used);
                        break;
                    case 1:
                        oldExp = info.attTexpExp;
                        info.attTexpExp += gain;
                        client.Player.OnUsingItem(45001, used);
                        text = "Tấn Công";
                        break;
                    case 2:
                        oldExp = info.defTexpExp;
                        info.defTexpExp += gain;
                        client.Player.OnUsingItem(45002, used);
                        text = "Phòng Thủ";
                        break;
                    case 3:
                        oldExp = info.spdTexpExp;
                        info.spdTexpExp += gain;
                        client.Player.OnUsingItem(45003, used);
                        text = "Nhanh Nhẹn";
                        break;
                    case 4:
                        oldExp = info.lukTexpExp;
                        info.lukTexpExp += gain;
                        client.Player.OnUsingItem(45004, used);
                        text = "May Mắn";
                        break;
                }
                info.texpCount += used;
                info.texpTaskCount += used;
                client.Player.PlayerCharacter.Texp = info;
                using (PlayerBussiness db = new PlayerBussiness())
                {
                    db.UpdateUserTexpInfo(info);
                }
                client.Player.EquipBag.UpdatePlayerProperties();

            }
            return 0;
        }
    }
}
