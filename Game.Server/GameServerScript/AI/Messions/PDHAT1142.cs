using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Effects;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.Messions;

public class PDHAT1142 : AMissionControl
{
	private SimpleBoss _dragonBoss;

	private SimpleBoss _barrelHelperBoss;

	private const int BarrelNpcId = 4101;

	private const int NpcId = 4103;

	private const int BossId = 4104;

	private readonly int _mBloodReduce = 1200;

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
		int[] npcIds = new int[3] { 4104, 4103, 4101 };
		base.Game.LoadResources(npcIds);
		base.Game.LoadNpcGameOverResources(npcIds);
		base.Game.AddLoadingFile(2, "image/game/effect/4/gate.swf", "game.asset.Gate");
		base.Game.SetMap(1142);
	}

	public override void OnPrepareStartGame()
	{
		base.OnPrepareStartGame();
		LivingConfig livingConfig = base.Game.BaseLivingConfig();
		livingConfig.CanTakeDamage = false;
		_dragonBoss = base.Game.CreateBoss(4104, 1520, 350, -1, 0, "", livingConfig);
		base.Game.SendHideBlood(_dragonBoss, 0);
		_dragonBoss.OnSmallMap(state: false);
	}

	public override void OnStartGame()
	{
		base.OnStartGame();
		base.Game.SendFreeFocus(_dragonBoss.X, _dragonBoss.Y, 1, 1000, 1500);
		base.Game.SendFreeFocus(335, 630, 1, 3000, 3500);
		_dragonBoss.CallFuction(CreateHelper, 4500);
	}

	public override void OnBeginNewTurn()
	{
		base.OnBeginNewTurn();
		if (_barrelHelperBoss != null && !_barrelHelperBoss.IsLiving)
		{
			base.Game.RemoveLiving(_barrelHelperBoss, sendToClient: true);
			CreateHelper();
		}
	}

	private void CreateHelper()
	{
		LivingConfig livingConfig = base.Game.BaseLivingConfig();
		livingConfig.IsHelper = true;
		livingConfig.CanFrost = true;
		_barrelHelperBoss = base.Game.CreateBoss(4101, 321, 746, 1, 0, "", livingConfig);
		_barrelHelperBoss.AddEffect(new ContinueReduceBloodEffect(3, _mBloodReduce, _barrelHelperBoss), 3000);
		_barrelHelperBoss.Delay++;
	}

	public override bool CanGameOver()
	{
		base.CanGameOver();
		return base.Game.TotalKillCount >= base.Game.MissionInfo.TotalCount || base.Game.TurnIndex >= base.Game.MissionInfo.TotalTurn;
	}

	public override int UpdateUIData()
	{
		base.UpdateUIData();
		return base.Game.TotalKillCount;
	}

	public override void OnGameOver()
	{
		base.OnGameOver();
		base.Game.IsWin = base.Game.TotalKillCount >= base.Game.MissionInfo.TotalCount;
	}
}
