using System;
using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.Messions;

public class WAH13204 : AMissionControl
{
	private int bossId = 13208;

	private int boss2Id = 13209;

	private int npc1Id = 13212;

	private int npc2Id = 13213;

	private int npc3Id = 13214;

	private int[] m_locationX = new int[16]
	{
		83, 90, 100, 110, 125, 135, 145, 265, 600, 850,
		990, 1400, 1550, 1620, 1760, 1910
	};

	private int m_turnMax = 2;

	private int m_turnCurrent = 0;

	private int m_turnReady = -1;

	private PhysicalObj m_moive;

	private PhysicalObj m_front;

	private SimpleBoss boss = null;

	private SimpleBoss boss2 = null;

	private SimpleNpc m_bombs;

	private int turnCreateBombs = 4;

	public override int CalculateScoreGrade(int score)
	{
		base.CalculateScoreGrade(score);
		if (score > 600)
		{
			return 3;
		}
		if (score > 520)
		{
			return 2;
		}
		if (score > 450)
		{
			return 1;
		}
		return 0;
	}

	public override void OnPrepareNewSession()
	{
		base.OnPrepareNewSession();
		int[] npcIds = new int[5] { bossId, boss2Id, npc1Id, npc2Id, npc3Id };
		base.Game.AddLoadingFile(2, "image/game/effect/10/danbao.swf", "asset.game.ten.danbao");
		base.Game.AddLoadingFile(2, "image/game/effect/10/qunbao.swf", "asset.game.ten.qunbao");
		base.Game.AddLoadingFile(2, "image/game/effect/10/tedabiaoji.swf", "asset.game.ten.tedabiaoji");
		base.Game.AddLoadingFile(2, "image/game/thing/BossBornBgAsset.swf", "game.asset.living.BossBgAsset");
		base.Game.AddLoadingFile(2, "image/game/thing/BossBornBgAsset.swf", "game.asset.living.shuangwangAsset");
		base.Game.LoadResources(npcIds);
		base.Game.LoadNpcGameOverResources(npcIds);
		base.Game.SetMap(1217);
	}

	public override void OnStartGame()
	{
		base.OnStartGame();
		m_moive = base.Game.Createlayer(0, 0, "moive", "game.asset.living.BossBgAsset", "out", 1, 0);
		m_front = base.Game.Createlayer(810, 750, "front", "game.asset.living.shuangwangAsset", "out", 1, 0);
		LivingConfig livingConfig = base.Game.BaseLivingConfig();
		livingConfig.IsShowBloodBar = true;
		livingConfig.IsShowBloodBar = true;
		livingConfig.FriendlyBoss = new LivingConfig.FriendlyLiving(boss2Id, cansharedamage: true);
		boss = base.Game.CreateBoss(bossId, 83, 1020, 1, 1, "", livingConfig);
		boss.SetRelateDemagemRect(boss.NpcInfo.X, boss.NpcInfo.Y, boss.NpcInfo.Width, boss.NpcInfo.Height);
		base.Game.SendObjectFocus(boss, 0, 0, 0);
		boss.CallFuction(CreateBossHelp, 2000);
	}

	private void CreateBossHelp()
	{
		LivingConfig livingConfig = base.Game.BaseLivingConfig();
		livingConfig.IsShowBloodBar = true;
		livingConfig.IsShowBloodBar = true;
		livingConfig.FriendlyBoss = new LivingConfig.FriendlyLiving(bossId, cansharedamage: true);
		boss2 = base.Game.CreateBoss(boss2Id, 1912, 1020, -1, 1, "", livingConfig);
		boss2.SetRelateDemagemRect(boss2.NpcInfo.X, boss2.NpcInfo.Y, boss2.NpcInfo.Width, boss2.NpcInfo.Height);
		boss2.Delay = 1;
		base.Game.SendObjectFocus(boss2, 0, 0, 0);
		base.Game.SendFreeFocus(1000, 900, 1, 2000, 0);
		m_moive.PlayMovie("in", 3000, 0);
		m_front.PlayMovie("in", 3200, 0);
		m_moive.PlayMovie("out", 6000, 0);
		m_front.PlayMovie("out", 6200, 0);
	}

	private void ReadyToBomb(SimpleBoss simpleBoss)
	{
		simpleBoss.Say("Tưởng vậy là xong rồi ư? Bọn ta sẽ tiễn các ngươi xuống địa ngục!", 1, 1000);
		simpleBoss.PlayMovie("die", 1900, 0);
		simpleBoss.Die(2800);
		foreach (Player allLivingPlayer in base.Game.GetAllLivingPlayers())
		{
			allLivingPlayer.SetSeal(state: true);
		}
	}

	private void CreateBombs()
	{
		base.Game.Shuffer(m_locationX);
		for (int i = 0; i < turnCreateBombs; i++)
		{
			LivingConfig livingConfig = base.Game.BaseLivingConfig();
			livingConfig.CanCountKill = false;
			int num = base.Game.Random.Next(0, 30);
			if (i >= m_locationX.Length)
			{
				Random random = new Random();
				m_bombs = base.Game.CreateNpc((num >= 0 && num <= 10) ? npc1Id : ((num >= 11 && num <= 20) ? npc2Id : npc3Id), m_locationX[random.Next(0, m_locationX.Length)], 1000, 0, -1);
			}
			else
			{
				m_bombs = base.Game.CreateNpc((num >= 0 && num <= 10) ? npc1Id : ((num >= 11 && num <= 20) ? npc2Id : npc3Id), m_locationX[i], 1000, 0, -1);
			}
			m_bombs.Properties1 = 1;
		}
	}

	public override void OnNewTurnStarted()
	{
		base.OnNewTurnStarted();
		if (m_turnReady != -1 && base.Game.FindAllNpcLiving().Length == 0)
		{
			m_turnReady++;
			if (m_turnReady > base.Game.GetAllLivingPlayers().Count)
			{
				CreateBombs();
				m_turnCurrent++;
				m_turnReady = 0;
			}
		}
	}

	public override void OnBeginNewTurn()
	{
		base.OnBeginNewTurn();
		if (m_moive != null && m_front != null)
		{
			base.Game.RemovePhysicalObj(m_moive, sendToClient: true);
			base.Game.RemovePhysicalObj(m_front, sendToClient: true);
			m_moive = null;
			m_front = null;
		}
	}

	public override bool CanGameOver()
	{
		base.CanGameOver();
		if (boss != null && !boss.IsLiving && m_turnReady == -1)
		{
			m_turnReady = 0;
			base.Game.SendFreeFocus(boss2.X, boss2.Y - 150, 0, 0, 0);
			boss2.CallFuction(delegate
			{
				ReadyToBomb(boss2);
			}, 1500);
			PhysicalObj[] array = base.Game.FindPhysicalObjByName("deadline");
			foreach (PhysicalObj phy in array)
			{
				base.Game.RemovePhysicalObj(phy, sendToClient: true);
			}
			base.Game.GameStateModify = eGameState.Waiting;
		}
		if (boss2 != null && !boss2.IsLiving && m_turnReady == -1)
		{
			m_turnReady = 0;
			base.Game.SendFreeFocus(boss.X, boss.Y - 150, 0, 0, 0);
			boss.CallFuction(delegate
			{
				ReadyToBomb(boss);
			}, 1500);
			PhysicalObj[] array2 = base.Game.FindPhysicalObjByName("deadline");
			foreach (PhysicalObj phy2 in array2)
			{
				base.Game.RemovePhysicalObj(phy2, sendToClient: true);
			}
			base.Game.GameStateModify = eGameState.Waiting;
		}
		if (m_turnCurrent >= m_turnMax && base.Game.FindAllNpcLiving().Length == 0)
		{
			return true;
		}
		if (base.Game.TotalTurn > 200)
		{
			return true;
		}
		return false;
	}

	public override void OnWaitingGameState()
	{
		base.OnWaitingGameState();
		base.Game.GameStateModify = eGameState.Playing;
	}

	public override void OnGameOver()
	{
		base.OnGameOver();
		if (m_turnCurrent >= m_turnMax && base.Game.FindAllNpcLiving().Length == 0)
		{
			base.Game.IsWin = true;
		}
		else
		{
			base.Game.IsWin = false;
		}
	}
}
