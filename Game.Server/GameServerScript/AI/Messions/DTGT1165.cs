using System.Collections.Generic;
using System.Drawing;
using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.Messions;

public class DTGT1165 : AMissionControl
{
	private SimpleNpc m_boss;

	private List<PhysicalObj> m_balls = new List<PhysicalObj>();

	private bool m_isDouble = false;

	private int m_doubleCount = 1;

	private int bossID = 6121;

	private int ballID = 6113;

	private PhysicalObj m_kingMoive;

	private PhysicalObj m_kingFront;

	private PhysicalObj m_diem_trai;

	private PhysicalObj hangdonvi_trai;

	private PhysicalObj hangchuc_trai;

	private PhysicalObj m_diem_phai;

	private PhysicalObj hangdonvi_phai;

	private PhysicalObj hangchuc_phai;

	private int turn = 0;

	private int[] birthX = new int[18]
	{
		450, 550, 650, 750, 850, 950, 1050, 1150, 1250, 455,
		555, 655, 755, 855, 955, 1055, 1155, 1255
	};

	private int[] birthY = new int[5] { 184, 259, 335, 420, 504 };

	private int solantangbongxanh = 4;

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
		int[] npcIds = new int[2] { bossID, ballID };
		base.Game.LoadResources(npcIds);
		base.Game.LoadNpcGameOverResources(npcIds);
		base.Game.AddLoadingFile(2, "image/game/thing/bossborn6.swf", "game.asset.living.GuizeAsset");
		base.Game.AddLoadingFile(2, "image/game/thing/BossBornBgAsset.swf", "game.asset.living.BossBgAsset");
		base.Game.AddLoadingFile(2, "image/game/effect/6/ball.swf", "asset.game.six.ball");
		base.Game.AddLoadingFile(2, "image/game/effect/6/jifenpai.swf", "asset.game.six.fenshu");
		base.Game.AddLoadingFile(2, "image/game/effect/6/jifenpai.swf", "asset.game.six.shuzi");
		base.Game.SetMap(1165);
	}

	public override void OnStartGame()
	{
		base.OnStartGame();
		base.Game.TotalCount = 99;
		base.Game.TotalTurn = base.Game.PlayerCount * 20;
		base.Game.SendMissionInfo();
		LivingConfig livingConfig = base.Game.BaseLivingConfig();
		livingConfig.CanTakeDamage = false;
		livingConfig.IsTurn = false;
		m_boss = base.Game.CreateNpc(bossID, 345, 860, 1, 1, "", livingConfig);
		m_boss.PlayMovie("standC", 0, 0);
		m_boss.PlayMovie("go", 1000, 0);
		m_boss.Say("Ô ! Ngưới mới à, chắc bạn không biết quy tắc ở đây.", 0, 0);
		m_boss.CallFuction(NextAttack2, 4000);
		m_boss.CallFuction(NextAttack, 5000);
	}

	private void NextAttack2()
	{
		m_kingMoive = base.Game.Createlayer(0, 0, "kingmoive", "game.asset.living.BossBgAsset", "out", 1, 1);
		base.Game.SendGameFocus(900, 500, 1, 0, 1000);
		m_kingMoive.PlayMovie("in", 0, 0);
		m_kingMoive.PlayMovie("out", 5000, 0);
		m_boss.CallFuction(CreatBall, 6000);
	}

	private void NextAttack()
	{
		m_kingFront = base.Game.Createlayer(900, 450, "font", "game.asset.living.GuizeAsset", "out", 1, 1);
		m_diem_trai = base.Game.Createlayer(170, 650, "movie", "asset.game.six.fenshu", "Z", 1, 1);
		hangchuc_trai = base.Game.Createlayer(270, 650, "movie", "asset.game.six.shuzi", "z0", 1, 1);
		hangdonvi_trai = base.Game.Createlayer(320, 650, "movie", "asset.game.six.shuzi", "z0", 1, 1);
		m_diem_phai = base.Game.Createlayer(1550, 650, "movie", "asset.game.six.fenshu", "Z", 1, 1);
		hangchuc_phai = base.Game.Createlayer(1650, 650, "movie", "asset.game.six.shuzi", "z0", 1, 1);
		hangdonvi_phai = base.Game.Createlayer(1700, 650, "movie", "asset.game.six.shuzi", "z0", 1, 1);
	}

	public List<string> chaysodonvi(int Totalcount)
	{
		string item = "";
		List<string> list = new List<string>();
		string text = Totalcount.ToString().Replace("-", "");
		string text2 = "0";
		string text3 = "0";
		if (Totalcount >= 0)
		{
			if (m_diem_trai != null)
			{
				m_diem_trai.PlayMovie("Z", 0, 900);
				m_diem_phai.PlayMovie("Z", 0, 900);
			}
		}
		else if (m_diem_trai != null)
		{
			m_diem_trai.PlayMovie("F", 0, 900);
			m_diem_phai.PlayMovie("F", 0, 900);
		}
		for (int i = 0; i < text.Length; i++)
		{
			if (text.Length == 2)
			{
				text2 = text[1].ToString();
				text3 = text[0].ToString();
			}
			else
			{
				text2 = text[0].ToString();
			}
		}
		switch (text3)
		{
		case "1":
			item = "z1";
			break;
		case "2":
			item = "z2";
			break;
		case "3":
			item = "z3";
			break;
		case "4":
			item = "z4";
			break;
		case "5":
			item = "z5";
			break;
		case "6":
			item = "z6";
			break;
		case "7":
			item = "z7";
			break;
		case "8":
			item = "z8";
			break;
		case "9":
			item = "z9";
			break;
		case "0":
			item = "z0";
			break;
		}
		list.Add(item);
		switch (text2)
		{
		case "1":
			item = "z1";
			break;
		case "2":
			item = "z2";
			break;
		case "3":
			item = "z3";
			break;
		case "4":
			item = "z4";
			break;
		case "5":
			item = "z5";
			break;
		case "6":
			item = "z6";
			break;
		case "7":
			item = "z7";
			break;
		case "8":
			item = "z8";
			break;
		case "9":
			item = "z9";
			break;
		case "0":
			item = "z0";
			break;
		}
		list.Add(item);
		return list;
	}

	private void CreatBall()
	{
		string[] array = new string[15]
		{
			"s1", "s2", "s3", "s4", "s5", "s1", "s2", "s3", "s4", "s5",
			"s1", "s2", "s3", "s4", "s5"
		};
		string[] array2 = new string[15]
		{
			"s-1", "s-2", "s-3", "s-4", "s-5", "s-1", "s-2", "s-3", "s-4", "s-5",
			"s-1", "s-2", "s-3", "s-4", "s-5"
		};
		string action = "double";
		Point[] array3 = new Point[21]
		{
			new Point(1001, 173),
			new Point(774, 203),
			new Point(639, 299),
			new Point(545, 463),
			new Point(566, 661),
			new Point(698, 805),
			new Point(858, 863),
			new Point(1091, 837),
			new Point(1223, 719),
			new Point(1307, 517),
			new Point(1289, 366),
			new Point(1171, 241),
			new Point(985, 240),
			new Point(717, 400),
			new Point(684, 602),
			new Point(853, 742),
			new Point(1140, 629),
			new Point(1145, 410),
			new Point(982, 438),
			new Point(829, 506),
			new Point(962, 592)
		};
		base.Game.Shuffer(array3);
		for (int i = 0; i < array3.Length; i++)
		{
			int num = base.Game.Random.Next(array2.Length);
			if (i < solantangbongxanh)
			{
				m_balls.Add(base.Game.CreateBall(array3[i].X, array3[i].Y, array2[num]));
				continue;
			}
			int num2 = base.Game.Random.Next(array.Length);
			m_balls.Add(base.Game.CreateBall(array3[i].X, array3[i].Y, array[num2]));
		}
		if (m_isDouble && m_doubleCount != 0)
		{
			int num3 = base.Game.Random.Next(0, array3.Length);
			m_balls[num3].PlayMovie(m_balls[num3].ActionMapping[m_balls[num3].CurrentAction], 0, 500);
			m_balls[num3].Die();
			base.Game.RemovePhysicalObj(m_balls[num3], sendToClient: true);
			m_balls[num3] = base.Game.CreateBall(array3[num3].X, array3[num3].Y, action);
			m_isDouble = false;
			m_doubleCount--;
		}
	}

	public override void OnNewTurnStarted()
	{
		base.OnNewTurnStarted();
		base.Game.ClearBall();
		if (base.Game.TurnIndex > 2)
		{
			int num = base.Game.Random.Next(4, 8);
			solantangbongxanh = num;
			int num2 = base.Game.Random.Next(1, 100);
			if (num2 > 80)
			{
				m_isDouble = true;
			}
		}
		CreatBall();
	}

	public override void OnBeginNewTurn()
	{
		base.OnBeginNewTurn();
		if (base.Game.TurnIndex > 1)
		{
			if (m_kingMoive != null)
			{
				base.Game.RemovePhysicalObj(m_kingMoive, sendToClient: true);
				m_kingMoive = null;
			}
			if (m_kingFront != null)
			{
				base.Game.RemovePhysicalObj(m_kingFront, sendToClient: true);
				m_kingFront = null;
			}
		}
	}

	public override bool CanGameOver()
	{
		base.CanGameOver();
		if (base.Game.TurnIndex > base.Game.PlayerCount * 10 && base.Game.TotalKillCount < 99)
		{
			return false;
		}
		if (base.Game.TotalKillCount >= 99)
		{
			base.Game.TotalKillCount = 99;
			return true;
		}
		return false;
	}

	public override int UpdateUIData()
	{
		if (base.Game.TotalKillCount < -99)
		{
			base.Game.TotalKillCount = -99;
		}
		if (hangdonvi_trai != null)
		{
			hangdonvi_trai.PlayMovie(chaysodonvi(base.Game.TotalKillCount)[1], 0, 1100);
			hangchuc_trai.PlayMovie(chaysodonvi(base.Game.TotalKillCount)[0], 0, 1100);
		}
		if (hangdonvi_phai != null)
		{
			hangdonvi_phai.PlayMovie(chaysodonvi(base.Game.TotalKillCount)[1], 0, 1100);
			hangchuc_phai.PlayMovie(chaysodonvi(base.Game.TotalKillCount)[0], 0, 1100);
		}
		return base.Game.TotalKillCount;
	}

	public override void OnGameOver()
	{
		base.OnGameOver();
		if (base.Game.TotalKillCount >= 99)
		{
			base.Game.IsWin = true;
		}
		else if (base.Game.TurnIndex > base.Game.PlayerCount * 10 && base.Game.TotalKillCount < 99)
		{
			base.Game.IsWin = false;
		}
	}

	public override void OnCalculatePoint(int point, bool isdouble)
	{
		base.Game.TotalKillCount += point;
		if (isdouble)
		{
			base.Game.TotalKillCount *= 2;
		}
		if (base.Game.TotalKillCount < -99)
		{
			base.Game.TotalKillCount = -99;
		}
		if (base.Game.TotalKillCount >= 99)
		{
			base.Game.TotalKillCount = 99;
		}
	}
}
