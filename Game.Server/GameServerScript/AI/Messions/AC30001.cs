using System.Collections.Generic;
using Bussiness;
using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.Messions;

public class AC30001 : AMissionControl
{
	private SimpleBoss boss = null;

	private int npcID = 30001;

	private int bossID = 30002;

	private SimpleNpc npc = null;

	private LivingConfig config;

	private List<SimpleNpc> someNpc = new List<SimpleNpc>();

	private int[] birthX = new int[10] { 443, 515, 683, 723, 800, 606, 785, 842, 910, 1075 };

	private double _damage = 0.0;

	public override int CalculateScoreGrade(int score)
	{
		base.CalculateScoreGrade(score);
		if (score > 1750)
		{
			return 3;
		}
		if (score > 1675)
		{
			return 2;
		}
		if (score > 1600)
		{
			return 1;
		}
		return 0;
	}

	public override void OnPrepareNewSession()
	{
		base.OnPrepareNewSession();
		int[] npcIds = new int[2] { npcID, bossID };
		int[] npcIds2 = new int[1] { npcID };
		base.Game.LoadResources(npcIds);
		base.Game.LoadNpcGameOverResources(npcIds2);
		base.Game.AddLoadingFile(2, "image/game/effect/0/294b.swf", "asset.game.zero.294b");
		base.Game.SetMap(1250);
	}

	public override void OnStartGame()
	{
		base.OnStartGame();
		config = base.Game.BaseLivingConfig();
		config.IsFly = true;
		config.IsWorldBoss = true;
		createNPC();
	}

	private void createNPC()
	{
		int[] array = birthX;
		foreach (int x in array)
		{
			int y = base.Game.Random.Next(478, 674);
			npc = base.Game.CreateNpc(npcID, x, y, 0, 1, config);
		}
	}

	public override void OnNewTurnStarted()
	{
		base.OnNewTurnStarted();
		if (base.Game.GetLivedLivings().Count < 3)
		{
			createNPC();
		}
	}

	public override void OnBeginNewTurn()
	{
		base.OnBeginNewTurn();
	}

	public override bool CanGameOver()
	{
		if (base.Game.TotalKillCount > 15 && boss == null)
		{
			base.Game.ClearAllChild();
			boss = base.Game.CreateBoss(bossID, 944, 481, -1, 1, "", config);
			boss.SetRelateDemagemRect(-200, -179, 272, 200);
			boss.Say(LanguageMgr.GetTranslation("Kẻ to gan nào lại dám đến lãnh địa của ta!!!"), 0, 200, 0);
			List<Player> allFightPlayers = base.Game.GetAllFightPlayers();
			Player player = base.Game.FindRandomPlayer();
			int num = 0;
			if (player != null)
			{
				num = player.Delay;
			}
			foreach (Player item in allFightPlayers)
			{
				if (item.Delay < num)
				{
					num = item.Delay;
				}
			}
			if (base.Game.TotalKillCount < 100)
			{
				_damage = 1000.0;
				boss.BaseDamage = _damage;
				_damage += 2500.0;
			}
			else
			{
				boss.State = 10;
			}
		}
		return false;
	}

	public override int UpdateUIData()
	{
		return base.Game.TotalKillCount;
	}

	public override void OnGameOver()
	{
		base.OnGameOver();
		if (boss != null && !boss.IsLiving)
		{
			base.Game.IsWin = true;
		}
		else
		{
			base.Game.IsWin = false;
		}
		List<Player> allFightPlayers = base.Game.GetAllFightPlayers();
		foreach (Player item in allFightPlayers)
		{
			item.PlayerDetail.UpdatePveResult("worldboss", item.TotalDameLiving, base.Game.IsWin);
		}
	}
}
