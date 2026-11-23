using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.NPC;

public class SeventhHardHouseAi : ABrain
{
	private int m_gatrongID = 7222;

	private int m_gamaiID = 7221;

	private int m_gamaiMaxCount = 3;

	private int m_gatrongMaxCount = 10;

	private int m_gamaiCount = 3;

	private int m_gatrongCount = 6;

	private bool m_halfBlood = false;

	public override void OnBeginSelfTurn()
	{
		base.OnBeginSelfTurn();
	}

	public override void OnBeginNewTurn()
	{
		base.OnBeginNewTurn();
		base.Body.CurrentDamagePlus = 1f;
		base.Body.CurrentShootMinus = 1f;
	}

	public override void OnCreated()
	{
		base.OnCreated();
	}

	public override void OnStartAttacking()
	{
		bool flag = false;
		foreach (Player allFightPlayer in base.Game.GetAllFightPlayers())
		{
			if (allFightPlayer.IsLiving && allFightPlayer.X > base.Body.X - 400 && allFightPlayer.X < base.Body.X + 400)
			{
				flag = true;
			}
		}
		if (flag)
		{
			KillAttack(base.Body.X - 400, base.Body.X + 400);
			return;
		}
		CreateNpc1();
		CreateNpc2();
	}

	public override void OnAfterTakedBomb()
	{
		base.OnAfterTakedBomb();
		if (base.Body.Blood <= base.Body.MaxBlood / 2 && !m_halfBlood)
		{
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "cry", "cryA");
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "stand", "standA");
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "to", "toA");
			m_halfBlood = true;
		}
		else if (base.Body.Blood >= base.Body.MaxBlood / 2)
		{
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "toA", "to");
		}
	}

	public override void OnStopAttacking()
	{
		base.OnStopAttacking();
	}

	public void KillAttack(int fx, int tx)
	{
		base.Body.RangeAttacking(fx, tx, "cry", 1000, null);
	}

	public void CreateNpc1()
	{
		if (m_gamaiCount > m_gamaiMaxCount)
		{
			m_gamaiCount = m_gamaiMaxCount;
		}
		int num = m_gamaiMaxCount - base.Game.GetLivedNpcs(m_gamaiID).Count;
		if (m_gamaiCount > num)
		{
			m_gamaiCount = num;
		}
		if (m_gamaiCount > 0)
		{
			for (int i = 0; i < m_gamaiCount; i++)
			{
				((SimpleBoss)base.Body).CreateChild(m_gamaiID, 596 + i * 75, 955, 0, 1, showBlood: true, ((PVEGame)base.Game).BaseLivingConfig());
			}
		}
	}

	public void CreateNpc2()
	{
		if (m_gatrongCount > m_gatrongMaxCount)
		{
			m_gatrongCount = m_gatrongMaxCount;
		}
		int num = m_gatrongMaxCount - base.Game.GetLivedNpcs(m_gatrongID).Count;
		if (m_gatrongCount > num)
		{
			m_gatrongCount = num;
		}
		if (m_gatrongCount > 0)
		{
			for (int i = 0; i < m_gatrongCount; i++)
			{
				((SimpleBoss)base.Body).CreateChild(m_gatrongID, 792 + i * 80, 950, 0, 1, showBlood: true, ((PVEGame)base.Game).BaseLivingConfig());
			}
		}
	}
}
