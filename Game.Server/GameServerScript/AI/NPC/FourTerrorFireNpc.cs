using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.NPC;

public class FourTerrorFireNpc : ABrain
{
	private readonly int shieldId = 4310;

	private SimpleNpc shield;

	public override void OnBeginSelfTurn()
	{
		base.OnBeginSelfTurn();
		if (shield != null && !shield.IsLiving)
		{
			shield = null;
			base.Body.Config.HaveShield = true;
		}
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
		base.Body.Properties1 = 0;
	}

	public override void OnStartAttacking()
	{
		base.OnStartAttacking();
		int num = base.Game.Random.Next(50, base.Game.Map.Info.DeadWidth - 50);
		int y = base.Game.Random.Next(50, 500);
		int delayCallback = base.Game.GetDelayDistance(base.Body.X, num, 10) + 800;
		if (shield == null)
		{
			base.Body.MoveTo(num, y, "fly", 10, "", 1000, CreateShield, delayCallback);
			return;
		}
		base.Body.MoveTo(num, y, "fly", 1000, 10);
		shield?.MoveTo(num, y, "fly", 1000, 10);
	}

	private void CreateShield()
	{
		LivingConfig livingConfig = ((PVEGame)base.Game).BaseLivingConfig();
		livingConfig.IsFly = true;
		livingConfig.CanCountKill = false;
		livingConfig.IsTurn = false;
		shield = ((PVEGame)base.Game).CreateNpc(shieldId, base.Body.X, base.Body.Y, 1, -1, livingConfig);
		shield.OnSmallMap(state: false);
		((PVEGame)base.Game).SendHideBlood(shield, 0);
		shield.OnSmallMap(state: false);
	}

	public override void OnAfterTakedBomb()
	{
		base.OnAfterTakedBomb();
	}

	public override void OnStopAttacking()
	{
		base.OnStopAttacking();
	}

	public override void OnBeforeTakedBomb()
	{
		base.OnBeforeTakedBomb();
		if (shield != null && !shield.IsLiving && base.Body.Config.HaveShield)
		{
			base.Body.Config.HaveShield = false;
		}
	}
}
