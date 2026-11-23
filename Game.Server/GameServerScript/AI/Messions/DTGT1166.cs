using System.Collections.Generic;
using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.Messions;

public class DTGT1166 : AMissionControl
{
	private SimpleBoss m_grace;

	private SimpleNpc m_captain;

	private List<SimpleNpc> m_chicken = new List<SimpleNpc>();

	private PhysicalObj m_front;

	private int IsSay = 0;

	private int graceID = 6123;

	private int redID = 6121;

	private int blueID = 6122;

	private int captainID = 6114;

	private static string[] KillChat = new string[2] { "Gửi cho bạn trở về nhà!", "Một mình, bạn có ảo tưởng có thể đánh bại tôi?" };

	private static string[] ShootedChat = new string[2] { " Đau ah! Đau ...", "Quốc vương vạn tuế ..." };

	public override int CalculateScoreGrade(int score)
	{
		base.CalculateScoreGrade(score);
		if (score > 900)
		{
			return 3;
		}
		if (score > 825)
		{
			return 2;
		}
		if (score > 725)
		{
			return 1;
		}
		return 0;
	}

	public override void OnPrepareNewSession()
	{
		base.OnPrepareNewSession();
		base.Game.AddLoadingFile(1, "bombs/86.swf", "tank.resource.bombs.Bomb86");
		base.Game.AddLoadingFile(2, "image/game/living/Living190.swf", "game.living.Living190");
		base.Game.AddLoadingFile(2, "image/game/effect/6/danti.swf", "asset.game.six.danti");
		base.Game.AddLoadingFile(2, "image/game/effect/6/cpdian.swf", "asset.game.six.cpdian");
		base.Game.AddLoadingFile(2, "image/game/effect/6/qunti.swf", "asset.game.six.qunti");
		base.Game.AddLoadingFile(2, "image/game/effect/6/qunjia.swf", "asset.game.six.qunjia");
		int[] npcIds = new int[4] { graceID, redID, blueID, captainID };
		base.Game.LoadResources(npcIds);
		base.Game.LoadNpcGameOverResources(npcIds);
		base.Game.SetMap(1166);
	}

	public override void OnPrepareStartGame()
	{
		base.OnPrepareStartGame();
		m_front = base.Game.Createlayer(1098, 1080, "hide", "game.living.Living190", "stand", 1, 0);
		CreateGraceAndCaptain();
	}

	public override void OnStartGame()
	{
		base.OnStartGame();
		base.Game.Param2 = 0;
		base.Game.SendLivingActionMapping(m_captain, "stand", "standC");
		m_captain.Say("Rồi, bây giờ thì đồng đội chúng ta phải gắng sức vượt qua đội xanh.", 0, 1000, 5000);
		m_captain.Say("Xuất phát nào!", 0, 5000);
		m_captain.PlayMovie("go", 5500, 0);
		CreateMember();
	}

	private void CreateGraceAndCaptain()
	{
		LivingConfig livingConfig = base.Game.BaseLivingConfig();
		livingConfig.CanTakeDamage = false;
		livingConfig.KeepLife = true;
		m_grace = base.Game.CreateBoss(graceID, 1950, 1080, -1, 1, "", livingConfig);
		m_grace.AddDelay(1000);
		LivingConfig livingConfig2 = base.Game.BaseLivingConfig();
		livingConfig2.CanTakeDamage = false;
		livingConfig2.IsTurn = false;
		livingConfig.KeepLife = true;
		m_captain = base.Game.CreateNpc(captainID, 494, 1080, 1, -1, "standC", livingConfig2);
		m_captain.BlockTurn = true;
	}

	private void CreateMember()
	{
		LivingConfig livingConfig = base.Game.BaseLivingConfig();
		livingConfig.IsHelper = true;
		livingConfig.IsFly = true;
		livingConfig.IsComplete = false;
		livingConfig.KeepLife = true;
		LivingConfig livingConfig2 = base.Game.BaseLivingConfig();
		livingConfig2.IsFly = true;
		livingConfig2.IsComplete = false;
		livingConfig2.KeepLife = true;
		int[] array = new int[10] { 1315, 1155, 995, 835, 675, 1395, 1235, 1075, 915, 755 };
		for (int i = 0; i < 10; i++)
		{
			if (i < 5)
			{
				m_chicken.Add(base.Game.CreateNpc(redID, 90, 1080, 0, 1, livingConfig));
			}
			else
			{
				m_chicken.Add(base.Game.CreateNpc(blueID, 90, 1080, 0, 1, livingConfig2));
			}
			m_chicken[i].Properties1 = i;
			m_chicken[i].Properties3 = array[i];
			m_chicken[i].IsHide = true;
		}
	}

	public override void OnNewTurnStarted()
	{
		base.OnNewTurnStarted();
	}

	public override void OnBeginNewTurn()
	{
		base.OnBeginNewTurn();
		IsSay = 0;
	}

	private void Ending()
	{
		base.Game.CanEndGame = true;
	}

	private void EndGame()
	{
		base.Game.SendFreeFocus(1100, 200, 0, 0, 2500);
		base.Game.SendFreeFocus(m_grace.X, m_grace.Y, 0, 3000, 6000);
		m_grace.PlayMovie((base.Game.TotalKillCount >= 5) ? "die" : "happy", 3000, 2500);
		m_grace.CallFuction(Ending, 3000);
	}

	public override bool CanGameOver()
	{
		base.CanGameOver();
		SimpleNpc[] nPCLivingWithID = base.Game.GetNPCLivingWithID(blueID);
		foreach (SimpleNpc simpleNpc in nPCLivingWithID)
		{
			if (simpleNpc.Y < 285)
			{
				simpleNpc.Config.IsComplete = true;
			}
		}
		SimpleNpc[] nPCLivingWithID2 = base.Game.GetNPCLivingWithID(redID);
		foreach (SimpleNpc simpleNpc2 in nPCLivingWithID2)
		{
			if (simpleNpc2.Y < 285)
			{
				simpleNpc2.Config.IsComplete = true;
			}
		}
		if (base.Game.TotalKillCount >= 5)
		{
			base.Game.TotalKillCount = 5;
			base.Game.Param2 = 0;
			return true;
		}
		if (base.Game.Param2 > 5)
		{
			base.Game.Param2 = 5;
		}
		if (base.Game.Param2 >= 5)
		{
			base.Game.TotalKillCount = 0;
			return true;
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
		if (base.Game.TotalKillCount >= 5)
		{
			base.Game.IsWin = true;
		}
		if (base.Game.Param2 >= 5)
		{
			base.Game.IsWin = false;
		}
	}

	public override void OnPrepareGameOver()
	{
		base.OnPrepareGameOver();
		EndGame();
	}
}
