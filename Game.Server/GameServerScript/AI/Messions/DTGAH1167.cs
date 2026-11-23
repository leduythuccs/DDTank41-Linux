using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.Messions;

public class DTGAH1167 : AMissionControl
{
	private SimpleBoss m_oaitu;

	private SimpleBoss m_trongtai;

	private SimpleNpc m_fan;

	private PhysicalObj m_kingMoive;

	private PhysicalObj m_front;

	private PhysicalObj m_decuatrongtai;

	private int IsSay = 0;

	private int m_oaituID = 6331;

	private int m_trongtaiID = 6332;

	private int m_fanID = 6334;

	private int m_decuatrongtaiID = 6335;

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
		base.Game.AddLoadingFile(1, "bombs/61.swf", "tank.resource.bombs.Bomb61");
		base.Game.AddLoadingFile(2, "image/game/thing/BossBornBgAsset.swf", "game.asset.living.BossBgAsset");
		base.Game.AddLoadingFile(2, "image/game/thing/BossBornBgAsset.swf", "game.asset.living.boguquanwangAsset");
		base.Game.AddLoadingFile(2, "image/game/effect/6/popcan.swf", "asset.game.six.popcan");
		base.Game.AddLoadingFile(2, "image/game/effect/6/popcan.swf", "popcan_fla.qusan_8");
		int[] npcIds = new int[4] { m_decuatrongtaiID, m_fanID, m_oaituID, m_trongtaiID };
		base.Game.LoadResources(npcIds);
		base.Game.LoadNpcGameOverResources(npcIds);
		base.Game.SetMap(1167);
	}

	public override void OnPrepareStartGame()
	{
		base.OnPrepareStartGame();
		LivingConfig livingConfig = base.Game.BaseLivingConfig();
		livingConfig.IsTurn = false;
		livingConfig.isShowBlood = false;
		livingConfig.IsFly = true;
		livingConfig.isShowSmallMapPoint = false;
		livingConfig.CanTakeDamage = false;
		m_fan = base.Game.CreateNpc(m_fanID, base.Game.Map.Info.DeadWidth / 2 - 3, base.Game.Map.Info.DeadHeight / 2 - 80, 0, -1, livingConfig);
		m_fan.PlayMovie("stand", 0, 0);
		m_fan.OnSmallMap(state: false);
		base.Game.SendHideBlood(m_fan, 1);
		m_decuatrongtai = base.Game.CreatePhysicalObj(base.Game.Map.Info.DeadWidth / 2, base.Game.Map.Info.DeadHeight / 2 + 100, "m_decuatrongtai", "game.living.Living181", "stand", 1, 0);
		m_decuatrongtai.SetRect(-126, -120, 260, 90);
		LivingConfig livingConfig2 = base.Game.BaseLivingConfig();
		livingConfig2.IsFly = true;
		livingConfig2.IsTurn = false;
		m_trongtai = base.Game.CreateBoss(m_trongtaiID, m_decuatrongtai.X + 20, m_decuatrongtai.Y - 100, 1, 1, "stand", livingConfig2);
		m_trongtai.PlayMovie("stand", 0, 0);
		m_trongtai.SetRect(m_trongtai.NpcInfo.X, m_trongtai.NpcInfo.Y, m_trongtai.NpcInfo.Width, m_trongtai.NpcInfo.Height);
		m_trongtai.SetRelateDemagemRect(m_trongtai.NpcInfo.X, m_trongtai.NpcInfo.Y, m_trongtai.NpcInfo.Width, m_trongtai.NpcInfo.Height);
		m_trongtai.Properties1 = 1;
		m_trongtai.Properties2 = 0;
	}

	public override void OnStartGame()
	{
		base.OnStartGame();
		m_trongtai.Say("Trong kỳ vận hội này chúng ta sẽ được chiêm ngưỡng sức mạnh của Quyền Vương và 4 đấu sĩ vô danh!", 1500, 0);
		LivingConfig livingConfig = base.Game.BaseLivingConfig();
		livingConfig.isShowBlood = false;
		livingConfig.KeepLife = true;
		m_oaitu = base.Game.CreateBoss(m_oaituID, base.Game.Map.Info.DeadWidth / 2 + 150, 975, -1, 6, "", livingConfig);
		m_oaitu.SetRelateDemagemRect(m_oaitu.NpcInfo.X, m_oaitu.NpcInfo.Y, m_oaitu.NpcInfo.Width, m_oaitu.NpcInfo.Height);
		m_oaitu.Properties2 = false;
		base.Game.SendFreeFocus(m_trongtai.X, m_trongtai.Y - 200, 0, 0, 2000);
		base.Game.SendFreeFocus(m_oaitu.X, m_oaitu.Y, 0, 6000, 0);
		m_trongtai.CallFuction(Layer, 6500);
	}

	private void Layer()
	{
		m_kingMoive = base.Game.Createlayer(0, 0, "kingmoive", "game.asset.living.BossBgAsset", "out", 1, 1);
		m_front = base.Game.Createlayer(m_oaitu.X - 50, m_oaitu.Y - 150, "font", "game.asset.living.boguquanwangAsset", "out", 1, 1);
		m_kingMoive.PlayMovie("in", 1000, 0);
		m_front.PlayMovie("in", 2000, 2000);
		foreach (Player allFightPlayer in base.Game.GetAllFightPlayers())
		{
			allFightPlayer.Properties1 = 0;
		}
	}

	public override void OnNewTurnStarted()
	{
		base.OnNewTurnStarted();
	}

	public override void OnBeginNewTurn()
	{
		base.OnBeginNewTurn();
		if (m_kingMoive != null)
		{
			base.Game.RemovePhysicalObj(m_kingMoive, sendToClient: true);
			m_kingMoive = null;
		}
		if (m_front != null)
		{
			base.Game.RemovePhysicalObj(m_front, sendToClient: true);
			m_front = null;
		}
		IsSay = 0;
	}

	public override bool CanGameOver()
	{
		base.CanGameOver();
		if (base.Game.TurnIndex > base.Game.MissionInfo.TotalTurn - 1)
		{
			return true;
		}
		if (!m_oaitu.IsLiving)
		{
			return true;
		}
		return false;
	}

	public override int UpdateUIData()
	{
		if (m_oaitu == null)
		{
			return 0;
		}
		if (!m_oaitu.IsLiving)
		{
			return 1;
		}
		return base.UpdateUIData();
	}

	public override void OnGameOver()
	{
		base.OnGameOver();
		if (!m_oaitu.IsLiving)
		{
			base.Game.IsWin = true;
		}
		else
		{
			base.Game.IsWin = false;
		}
	}
}
