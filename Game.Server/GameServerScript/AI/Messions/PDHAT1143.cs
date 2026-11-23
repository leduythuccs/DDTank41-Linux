using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.Messions;

public class PDHAT1143 : AMissionControl
{
	private SimpleBoss _mHawkBoss;

	private SimpleBoss _mBoss;

	private readonly int _bossId = 4105;

	private readonly int _bossId2 = 4106;

	private bool m_canWin = false;

	private PhysicalObj _mMoive;

	private PhysicalObj _mFront;

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
		int[] npcIds = new int[2] { _bossId, _bossId2 };
		int[] npcIds2 = new int[2] { _bossId, _bossId2 };
		base.Game.AddLoadingFile(2, "image/game/effect/4/feather.swf", "asset.game.4.feather");
		base.Game.AddLoadingFile(2, "image/game/thing/bossbornbgasset.swf", "game.asset.living.BossBgAsset");
		base.Game.AddLoadingFile(2, "image/game/thing/bossbornbgasset.swf", "game.asset.living.tingyuanlieshouAsset");
		base.Game.LoadResources(npcIds);
		base.Game.LoadNpcGameOverResources(npcIds2);
		base.Game.SetMap(1143);
	}

	public override void OnStartGame()
	{
		base.OnStartGame();
		_mMoive = base.Game.Createlayer(0, 0, "moive", "game.asset.living.BossBgAsset", "out", 1, 0);
		_mFront = base.Game.Createlayer(658, 607, "front", "game.asset.living.tingyuanlieshouAsset", "out", 1, 0);
		LivingConfig livingConfig = base.Game.BaseLivingConfig();
		livingConfig.IsFly = true;
		livingConfig.IsShowBloodBar = true;
		livingConfig.FriendlyBoss = new LivingConfig.FriendlyLiving(_bossId2, cansharedamage: true);
		_mHawkBoss = base.Game.CreateBoss(_bossId, 354, 344, -1, 1, "", livingConfig);
		_mHawkBoss.SetRelateDemagemRect(_mHawkBoss.NpcInfo.X, _mHawkBoss.NpcInfo.Y, _mHawkBoss.NpcInfo.Width, _mHawkBoss.NpcInfo.Height);
		_mHawkBoss.DoAction = 2;
		base.Game.SendObjectFocus(_mHawkBoss, 1, 100, 0);
		base.Game.SendFreeFocus(1460, 962, 1, 3000, 0);
		_mHawkBoss.CallFuction(CreateBoss, 4000);
	}

	private void CreateBoss()
	{
		LivingConfig livingConfig = base.Game.BaseLivingConfig();
		livingConfig.IsShowBloodBar = true;
		livingConfig.FriendlyBoss = new LivingConfig.FriendlyLiving(_bossId, cansharedamage: true);
		_mBoss = base.Game.CreateBoss(_bossId2, 1460, 962, -1, 1, "", livingConfig);
		_mBoss.SetRelateDemagemRect(_mBoss.NpcInfo.X, _mBoss.NpcInfo.Y, _mBoss.NpcInfo.Width, _mBoss.NpcInfo.Height);
		_mBoss.DoAction = 2;
		_mBoss.Delay++;
		base.Game.SendFreeFocus(740, 680, 1, 2500, 0);
		_mBoss.Config.FriendlyBoss.FriendBoss = _mHawkBoss;
		_mBoss.Config.FriendlyBoss.ActionStr = "shield";
		_mHawkBoss.Config.FriendlyBoss.FriendBoss = _mBoss;
		_mHawkBoss.Config.FriendlyBoss.ActionStr = "shield";
		_mMoive.PlayMovie("in", 3000, 0);
		_mFront.PlayMovie("in", 3200, 0);
		_mMoive.PlayMovie("out", 6000, 0);
		_mFront.PlayMovie("out", 6000, 0);
	}

	public override void OnBeginNewTurn()
	{
		base.OnBeginNewTurn();
		if (base.Game.TurnIndex > 1)
		{
			if (_mMoive != null)
			{
				base.Game.RemovePhysicalObj(_mMoive, sendToClient: true);
				_mMoive = null;
			}
			if (_mFront != null)
			{
				base.Game.RemovePhysicalObj(_mFront, sendToClient: true);
				_mFront = null;
			}
		}
	}

	public override bool CanGameOver()
	{
		if (_mBoss != null && !_mBoss.IsLiving && !m_canWin)
		{
			base.Game.GameStateModify = eGameState.Waiting;
			base.Game.SendFreeFocus(_mHawkBoss.X, _mHawkBoss.Y - 100, 0, 0, 0);
			_mHawkBoss.PlayMovie("die", 2500, 0);
			_mHawkBoss.Die(4000);
			m_canWin = true;
		}
		if (_mHawkBoss != null && !_mHawkBoss.IsLiving && !m_canWin)
		{
			base.Game.GameStateModify = eGameState.Waiting;
			base.Game.SendFreeFocus(_mBoss.X, _mBoss.Y - 100, 0, 0, 0);
			_mBoss.PlayMovie("die", 2500, 0);
			_mBoss.Die(4000);
			m_canWin = true;
		}
		return base.Game.TotalKillCount >= base.Game.MissionInfo.TotalCount || base.Game.TotalTurn > base.Game.MissionInfo.TotalTurn;
	}

	public override int UpdateUIData()
	{
		base.UpdateUIData();
		return base.Game.TotalKillCount;
	}

	public override void OnWaitingGameState()
	{
		base.OnWaitingGameState();
		base.Game.GameStateModify = eGameState.Playing;
	}

	public override void OnGameOver()
	{
		base.OnGameOver();
		base.Game.IsWin = base.Game.TotalKillCount >= base.Game.MissionInfo.TotalCount;
	}
}
