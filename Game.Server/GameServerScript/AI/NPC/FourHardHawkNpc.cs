using System.Collections.Generic;
using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.NPC;

public class FourHardHawkNpc : ABrain
{
	private int _mAttackTurn;

	private List<PhysicalObj> _mFeatherEffect = new List<PhysicalObj>();

	private readonly int _friendlyNpcId = 4206;

	private int m_totalNpc = 4;

	private int npcId = 4202;

	private SimpleBoss _friendBoss;

	public override void OnBeginNewTurn()
	{
		base.OnBeginNewTurn();
		ClearFeatherEffect();
		base.Body.CurrentDamagePlus = 1f;
		base.Body.CurrentShootMinus = 1f;
		base.Body.Config.HaveShield = base.Body.Properties1 == 0;
		SetState();
	}

	public override void OnCreated()
	{
		base.OnCreated();
		base.Body.Properties1 = 0;
		SetState();
	}

	public override void OnStartAttacking()
	{
		base.Game.ShowBloodItem(base.Body.Id);
		switch (_mAttackTurn)
		{
		case 0:
			if (base.Body.Properties1 == 0)
			{
				FlyRandom(AllAttackFeather);
			}
			_mAttackTurn++;
			break;
		case 1:
			if (base.Body.Properties1 == 0)
			{
				FlyRandom(AllAttackFeather);
			}
			_mAttackTurn++;
			break;
		case 2:
			if (base.Body.Properties1 == 0)
			{
				base.Body.Properties1 = 1;
				FlyRandom(ChangeAtoB);
			}
			else
			{
				base.Body.Properties1 = 0;
				ChangeBtoA();
			}
			_mAttackTurn = 0;
			break;
		}
	}

	public override void OnDie()
	{
		base.OnDie();
	}

	private void GetFriendBoss()
	{
		SimpleBoss[] array = base.Game.FindLivingTurnBossWithID(_friendlyNpcId);
		int num = 0;
		if (num < array.Length)
		{
			SimpleBoss friendBoss = array[num];
			_friendBoss = friendBoss;
		}
	}

	private void FlyRandom(LivingCallBack callBack)
	{
		int num = base.Game.Random.Next(400, 1283);
		int y = base.Game.Random.Next(400, 654);
		int delayDistance = base.Game.GetDelayDistance(base.Body.X, num, 7);
		base.Body.MoveTo(num, y, "fly", 7, "", 2000, callBack, delayDistance / 100 * 80);
	}

	private void CreateNpc()
	{
		for (int i = 0; i < m_totalNpc; i++)
		{
			int num = base.Body.X + base.Game.Random.Next(-300, 300);
			int num2 = base.Body.Y + base.Game.Random.Next(0, 300);
			num = ((num >= 0) ? num : 0);
			num = ((num > base.Game.Map.Info.DeadWidth) ? base.Game.Map.Info.DeadWidth : num);
			num2 = ((num2 > 778) ? 778 : num2);
			LivingConfig livingConfig = ((PVEGame)base.Game).BaseLivingConfig();
			livingConfig.CanCountKill = false;
			livingConfig.CanTakeDamage = false;
			livingConfig.IsFly = true;
			((SimpleBoss)base.Body).CreateChild(npcId, num, num2, showBlood: false, livingConfig);
		}
		base.Body.CallFuction(SetState, 2000);
	}

	private void RemoveAllNpc()
	{
		((SimpleBoss)base.Body).RemoveAllChild();
	}

	private void AllAttackFeather()
	{
		Player player = base.Game.FindRandomPlayer();
		base.Body.ChangeDirection(player, 0);
		base.Body.PlayMovie("beatA", 1000, 0);
		((PVEGame)base.Game).SendObjectFocus(player, 1, 2000, 0);
		base.Body.CallFuction(CreateFeatherEffect, 2500);
		base.Body.RangeAttacking(0, base.Game.Map.Info.DeadWidth, "cry", 2600, null);
	}

	private void CreateFeatherEffect()
	{
		foreach (Player allLivingPlayer in base.Game.GetAllLivingPlayers())
		{
			_mFeatherEffect.Add(((PVEGame)base.Game).Createlayer(allLivingPlayer.X, allLivingPlayer.Y, "", "asset.game.4.feather", "", 1, 0));
		}
	}

	private void ClearFeatherEffect()
	{
		foreach (PhysicalObj item in _mFeatherEffect)
		{
			base.Game.RemovePhysicalObj(item, sendToClient: true);
		}
		_mFeatherEffect = new List<PhysicalObj>();
	}

	private void ChangeAtoB()
	{
		base.Body.PlayMovie("AtoB", 500, 0);
		base.Body.CallFuction(CreateNpc, 3500);
		SetState();
	}

	private void ChangeBtoA()
	{
		base.Body.PlayMovie("BtoA", 1000, 0);
		RemoveAllNpc();
		SetState();
	}

	private void SetState()
	{
		int properties = base.Body.Properties1;
		int num = properties;
		if (num == 1)
		{
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "stand", "standB");
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "cry", "cry");
		}
		else
		{
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "stand", "standA");
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "cry", "shield");
		}
	}
}
