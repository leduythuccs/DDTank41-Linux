using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.NPC;

public class FourNormalShortNpc : ABrain
{
	private const int BossId = 4101;

	private SimpleBoss m_barrelBoss;

	private static readonly string[] NormalSay = new string[5] { "Thằng khốn nạn nào quăng mình ra đây thế này?", "Đây là đâu? Sao nóng thế?", "Tại sao mình lại ở đây nhỉ?", "Chuyện gì đang xảy ra vậy?", "Thật không thể hiểu nổi!" };

	public override void OnBeginNewTurn()
	{
		base.OnBeginNewTurn();
		if (m_body.IsSay)
		{
			m_body.Say(NormalSay[base.Game.Random.Next(0, NormalSay.Length)], 0, base.Game.Random.Next(0, 5000));
		}
	}

	public override void OnStartAttacking()
	{
		base.OnStartAttacking();
		m_body.CurrentDamagePlus = 1f;
		m_body.CurrentShootMinus = 1f;
		if (m_barrelBoss == null)
		{
			SetBarrelBoss();
		}
		else if (!m_barrelBoss.IsLiving)
		{
			SetBarrelBoss();
		}
		if (m_barrelBoss != null)
		{
			base.Body.MoveTo(m_barrelBoss.X + base.Game.Random.Next(50, 150), m_barrelBoss.Y, "walk", 1500, ((SimpleNpc)base.Body).NpcInfo.speed);
		}
	}

	private void SetBarrelBoss()
	{
		m_barrelBoss = ((PVEGame)base.Game).FindBossWithID(4101);
	}
}
