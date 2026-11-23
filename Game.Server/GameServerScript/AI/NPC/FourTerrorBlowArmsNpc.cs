using System.Collections.Generic;
using System.Drawing;
using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.NPC;

public class FourTerrorBlowArmsNpc : ABrain
{
	private int m_attackTurn = 0;

	private PhysicalObj m_Moive;

	private SimpleNpc m_npcDamage;

	private const int MTotalNpc = 1;

	private readonly List<SimpleNpc> _npcs = new List<SimpleNpc>();

	private const int NpcId = 4303;

	private SimpleBoss m_dragonBoss;

	private const int BossId = 4304;

	private bool m_showWarning;

	private bool ShowWarning
	{
		get
		{
			return m_showWarning;
		}
		set
		{
			m_showWarning = value;
			SetState();
		}
	}

	public override void OnBeginSelfTurn()
	{
	}

	public override void OnBeginNewTurn()
	{
		base.OnBeginNewTurn();
		m_body.CurrentDamagePlus = 1f;
		m_body.CurrentShootMinus = 1f;
		if (m_dragonBoss == null)
		{
			SetDragonBoss();
		}
		if (m_dragonBoss.Properties1 == 1 && !ShowWarning)
		{
			ShowWarning = true;
		}
		else if (m_dragonBoss.Properties1 == 0 && ShowWarning)
		{
			ShowWarning = false;
		}
	}

	public override void OnStartAttacking()
	{
		base.OnStartAttacking();
		Point point = default(Point);
		ShowWarning = false;
		switch (m_attackTurn)
		{
		case 0:
			(base.Game as PVEGame)?.SendObjectFocus(base.Body, 1, 0, 0);
			point = new Point(672, 746);
			break;
		case 1:
			point = new Point(1059, 749);
			break;
		case 2:
			point = new Point(1412, 751);
			break;
		default:
			point = new Point(1412, 751);
			break;
		}
		int num = int.MaxValue;
		SimpleNpc[] array = base.Game.FindAllNpcLiving();
		foreach (SimpleNpc simpleNpc in array)
		{
			if (simpleNpc.IsLiving && simpleNpc.X >= base.Body.X && simpleNpc.X <= base.Body.X + point.X)
			{
				int num2 = (int)base.Body.Distance(simpleNpc.X, simpleNpc.Y);
				if (num2 < num)
				{
					m_npcDamage = simpleNpc;
					num = num2;
				}
			}
		}
		if (m_npcDamage != null && m_npcDamage.Y > base.Body.X)
		{
			MoveToPlace(m_npcDamage.X - 20, m_npcDamage.Y, BeatNpc);
		}
		else if (m_attackTurn < 2)
		{
			MoveToPlace(point.X, point.Y, CreateNpc);
		}
		else
		{
			MoveToPlace(point.X, point.Y, StartBomber);
			m_attackTurn = 0;
		}
		m_attackTurn++;
	}

	private void StandB()
	{
		base.Body.PlayMovie("standB", 0, 0);
	}

	private void SetState()
	{
		((PVEGame)base.Game).SendLivingActionMapping(base.Body, "stand", m_showWarning ? "standB" : "stand");
	}

	private void MoveToPlace(int x, int y, LivingCallBack callBack)
	{
		base.Body.MoveTo(x, y, "walk", 1000, "", 5, callBack);
	}

	public void BeatNpc()
	{
		base.Body.Beat(m_npcDamage, "die", 5000, 5000, 800);
		base.Body.Die(3000);
	}

	private void StartBomber()
	{
		base.Body.EffectList.StopAllEffect();
		(base.Game as PVEGame)?.SendHideBlood(base.Body, 0);
		base.Body.PlayMovie("beatA", 2000, 6000);
		base.Body.CallFuction(CreateCrashGate, 4500);
	}

	private void CreateCrashGate()
	{
		switch (((PVEGame)base.Game).TotalKillCount)
		{
		case 0:
			if (m_Moive == null)
			{
				m_Moive = ((PVEGame)base.Game).Createlayer(1590, 750, "", "game.asset.Gate", "cryA", 1, 0);
			}
			else
			{
				m_Moive.PlayMovie("cryA", 0, 0);
			}
			break;
		case 1:
			if (m_Moive == null)
			{
				m_Moive = ((PVEGame)base.Game).Createlayer(1590, 750, "", "game.asset.Gate", "cryB", 1, 0);
			}
			else
			{
				m_Moive.PlayMovie("cryB", 0, 0);
			}
			break;
		case 2:
			if (m_Moive == null)
			{
				m_Moive = ((PVEGame)base.Game).Createlayer(1590, 750, "", "game.asset.Gate", "cryC", 1, 0);
			}
			else
			{
				m_Moive.PlayMovie("cryC", 0, 0);
			}
			break;
		}
		((PVEGame)base.Game).TotalKillCount++;
		base.Body.Die();
	}

	private void SetDragonBoss()
	{
		m_dragonBoss = ((PVEGame)base.Game).FindBossWithID(4304);
	}

	private void CreateNpc()
	{
		if (base.Game.GetLivedNpcs(4303).Count <= 0)
		{
			LivingConfig livingConfig = ((PVEGame)base.Game).BaseLivingConfig();
			livingConfig.CanFrost = false;
			livingConfig.CanCountKill = false;
			for (int i = 0; i < 1; i++)
			{
				((PVEGame)base.Game).CreateNpc(4303, base.Body.X + base.Game.Random.Next(-50, 150), base.Body.Y, 0, -1, livingConfig);
			}
		}
	}
}
