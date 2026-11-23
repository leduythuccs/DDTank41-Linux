using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.NPC;

public class FourNormalGunNpc : ABrain
{
	private bool m_isOpenEye;

	private SimpleBoss m_barrel;

	private const int BarrelId = 4101;

	public override void OnBeginNewTurn()
	{
		base.OnBeginNewTurn();
		m_body.CurrentDamagePlus = 1f;
		m_body.CurrentShootMinus = 1f;
	}

	public override void OnCreated()
	{
		base.OnCreated();
		base.Body.Properties1 = 0;
	}

	public override void OnStartAttacking()
	{
		base.OnStartAttacking();
		bool flag = false;
		foreach (Player allFightPlayer in base.Game.GetAllFightPlayers())
		{
			if (allFightPlayer.IsLiving && allFightPlayer.X > base.Body.X - 300 && allFightPlayer.X < base.Body.X + 300)
			{
				flag = true;
			}
		}
		if (flag)
		{
			KillAttack(base.Body.X - 300, base.Body.X + 300);
		}
		else if (!m_isOpenEye)
		{
			base.Body.Properties1 = 1;
			m_isOpenEye = true;
			SetStand();
			((PVEGame)base.Game).SendFreeFocus(base.Body.X, base.Body.Y, 1, 100, 0);
			base.Body.PlayMovie("beatA", 1000, 0);
		}
		else
		{
			base.Body.Properties1 = 0;
			m_isOpenEye = false;
			SetStand();
			((PVEGame)base.Game).SendFreeFocus(base.Body.X, base.Body.Y, 1, 100, 0);
			base.Body.PlayMovie("beatB", 1000, 0);
			((PVEGame)base.Game).SendFreeFocus(1305, 655, 2, 1000, 500);
			base.Body.RangeAttacking(475, 1900, "cry", 4000, directDamage: false);
			((PVEGame)base.Game).SendFreeFocus(900, 700, 1, 2000, 500);
			base.Body.CallFuction(BarrelDamage, 4000);
		}
	}

	private void BarrelDamage()
	{
		if (m_barrel == null)
		{
			SetBarrelBoss();
		}
		else if (!m_barrel.IsLiving)
		{
			SetBarrelBoss();
		}
		if (m_barrel.X >= 475)
		{
			if (m_barrel.IsFrost)
			{
				m_barrel.IsFrost = false;
				return;
			}
			m_barrel.EffectList.StopAllEffect();
			((PVEGame)base.Game).SendHideBlood(m_barrel, 0);
			m_barrel.PlayMovie("die", 0, 0);
			m_barrel.Die(2000);
		}
	}

	private void SetBarrelBoss()
	{
		m_barrel = ((PVEGame)base.Game).FindBossWithID(4101);
	}

	private void KillAttack(int fx, int tx)
	{
		base.Body.CurrentDamagePlus = 1000f;
		base.Body.PlayMovie("beatC", 1000, 0);
		base.Body.RangeAttacking(fx, tx, "cry", 2000, null);
	}

	private void SetStand()
	{
		((PVEGame)base.Game).SendLivingActionMapping(base.Body, "stand", m_isOpenEye ? "standB" : "stand");
	}
}
