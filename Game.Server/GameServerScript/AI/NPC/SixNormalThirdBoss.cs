using System.Collections.Generic;
using System.Linq;
using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Effects;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.NPC;

public class SixNormalThirdBoss : ABrain
{
	private Player m_target = null;

	private PhysicalObj m_popcan = null;

	private int m_hiep = 0;

	private int m_attackTurn = 0;

	private int m_buffGuard = 0;

	private int m_buffAttack = 0;

	private Player FindUntargetPlayer()
	{
		List<Player> list = new List<Player>();
		List<Player> list2 = new List<Player>();
		foreach (Player allLivingPlayer in base.Game.GetAllLivingPlayers())
		{
			if (allLivingPlayer.IsLiving)
			{
				list.Add(allLivingPlayer);
			}
		}
		list2 = list.OrderBy((Player p) => p.Properties1).ToList();
		if (list2.Count > 0)
		{
			return list2[0];
		}
		return null;
	}

	public override void OnBeginSelfTurn()
	{
		base.OnBeginSelfTurn();
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
	}

	public override void OnStartAttacking()
	{
		base.OnStartAttacking();
		SimpleBoss[] array = base.Game.FindLivingTurnBossWithID(6132);
		int num = 0;
		int num2 = 0;
		if ((int)array[0].Properties2 == 1)
		{
			array[0].Properties2 = 0;
			if (base.Body.Properties1 == 1)
			{
				array[0].Say("Sao mặt đỏ thế? Dùng chất kích thích à!", 0, 0);
				array[0].PlayMovie("beat", 1000, 0);
				((PVEGame)base.Game).SendGameFocus(base.Body, 3500, 0);
				base.Body.Properties1 = 0;
				base.Body.IconPicture(eMirariType.Guard, result: false);
				base.Body.IconPicture(eMirariType.Damage, result: false);
				base.Body.Attack = (base.Body as SimpleBoss).NpcInfo.Attack;
				base.Body.Defence = (base.Body as SimpleBoss).NpcInfo.Defence;
				base.Body.CallFuction(CreateEffectQusan, 4000);
				num2 = 6000;
			}
		}
		if (array[0].Properties1 == 1)
		{
			num = 2000 + num2;
			((PVEGame)base.Game).SendGameFocus(array[0], 0, num2);
			array[0].PlayMovie("beida", num2 + 500, 2000);
			array[0].Properties1 = 0;
		}
		if (base.Body.Blood > 1)
		{
			if ((bool)base.Body.Properties2)
			{
				base.Body.CallFuction(CreateEffectGreen, num);
			}
			else
			{
				int num3 = base.Game.Random.Next(0, 100);
				LivingCallBack livingCallBack = null;
				livingCallBack = ((num3 < 50) ? new LivingCallBack(CreateEffectBlue) : new LivingCallBack(CreateEffectRed));
				if (m_attackTurn == 0)
				{
					MoveToTarget(PunchBeatE, num, num + 1000);
					m_attackTurn++;
				}
				else if (m_attackTurn == 1)
				{
					base.Body.CallFuction(livingCallBack, num);
					MoveToTarget(PunchBeatF, num + 2800, num + 3800);
					m_attackTurn++;
				}
				else if (m_attackTurn == 2)
				{
					base.Body.CallFuction(livingCallBack, num);
					MoveToTarget(PunchBeatA, num + 2800, num + 3800);
					m_attackTurn++;
				}
				else if (m_attackTurn == 3)
				{
					MoveToTarget(PunchBeatA, num, num + 1000);
					m_attackTurn = 0;
				}
			}
			SetState();
		}
		else if (m_hiep == 0)
		{
			((PVEGame)base.Game).SendFreeFocus(array[0].X, array[0].Y - 150, 0, 0, 0);
			array[0].Say("Hả? Quyền Vương gục rồi sao?!", 0, 800);
			array[0].PlayMovie("beat", 1000, 0);
			array[0].Properties1 = 1;
			((PVEGame)base.Game).SendGameFocus(base.Body, 2000, 0);
			base.Body.PlayMovie("dieA", 3000, 0);
			base.Body.CallFuction(ResetBlood, 6400);
			((PVEGame)base.Game).SendGameFocus(array[0], 7500, 0);
			array[0].PlayMovie("beida", 8000, 2000);
			array[0].Properties1 = 0;
			m_hiep = 1;
		}
		else
		{
			((PVEGame)base.Game).SendFreeFocus(array[0].X, array[0].Y - 150, 0, 0, 0);
			array[0].Say("Hả? Quyền Vương gục rồi sao?!", 0, 800);
			array[0].PlayMovie("beat", 1000, 0);
			((PVEGame)base.Game).SendGameFocus(base.Body, 2000, 0);
			base.Body.PlayMovie("dieB", 3000, 0);
			array[0].Say("Ôi! Gục rồi..........", 0, 6500);
			((PVEGame)base.Game).SendFreeFocus(array[0].X, array[0].Y - 150, 0, 6000, 0);
			array[0].PlayMovie("beat", 6800, 0);
			base.Body.Die(6700);
			m_hiep = 2;
		}
	}

	private void CreateEffectQusan()
	{
		m_popcan = ((PVEGame)base.Game).Createlayer(base.Body.X - 150, base.Body.Y + 120, "blue", "popcan_fla.qusan_8", "", 1, 0);
		base.Body.CallFuction(RemoveEffect, 1200);
	}

	private void ResetBlood()
	{
		base.Body.AddBlood(base.Body.MaxBlood);
		SetState();
	}

	private void CreateEffectBlue()
	{
		m_popcan = ((PVEGame)base.Game).Createlayer(base.Body.X, base.Body.Y, "blue", "asset.game.six.popcan", "blueB", 1, 0);
		base.Body.IconPicture(eMirariType.Guard, result: true);
		base.Body.Properties1 = 1;
		m_buffGuard += 1000;
		base.Body.Defence += m_buffGuard;
		base.Body.CallFuction(RemoveEffect, 2500);
	}

	private void CreateEffectGreen()
	{
		m_popcan = ((PVEGame)base.Game).Createlayer(base.Body.X, base.Body.Y, "green", "asset.game.six.popcan", "greenB", 1, 0);
		base.Body.CallFuction(RemoveEffect, 2500);
	}

	private void CreateEffectRed()
	{
		m_popcan = ((PVEGame)base.Game).Createlayer(base.Body.X, base.Body.Y, "red", "asset.game.six.popcan", "redB", 1, 0);
		base.Body.IconPicture(eMirariType.Damage, result: true);
		base.Body.Properties1 = 1;
		m_buffAttack += 1000;
		base.Body.Attack += m_buffAttack;
		base.Body.CallFuction(RemoveEffect, 2500);
	}

	private void PunchBeatF()
	{
		base.Body.BeatDirect(m_target, "beatF", 0, 1, 1);
		base.Body.CallFuction(PlayerMove, 1500);
	}

	private void PlayerMove()
	{
		if (base.Body.X > m_target.X)
		{
			m_target.StartSpeedMult((m_target.X - 250 < 550) ? 550 : (m_target.X - 250), m_target.Y, 2000);
		}
		else
		{
			m_target.StartSpeedMult((m_target.X + 250 > 1965) ? 1965 : (m_target.X + 250), m_target.Y, 2000);
		}
		m_target.StartFalling(direct: true, 3000, 1000);
	}

	private void PunchBeatA()
	{
		base.Body.FallFrom(base.Body.X, base.Body.Y, "", 0, 0, 1000);
		base.Body.ChangeDirection(m_target, 0);
		base.Body.PlayMovie("beatA", 0, 0);
		if (base.Body.Direction == -1)
		{
			base.Body.RangeAttacking(base.Body.X - 180, base.Body.X, "cry", 500, directDamage: false);
		}
		else
		{
			base.Body.RangeAttacking(base.Body.X, base.Body.X + 180, "cry", 500, directDamage: false);
		}
		base.Body.CallFuction(PunchBeatB, 1000);
	}

	private void PunchBeatB()
	{
		m_target = FindUntargetPlayer();
		if (m_target != null)
		{
			if (base.Body.X > m_target.X)
			{
				base.Body.BoltMove(m_target.X + 150, base.Body.Y, 0);
			}
			else
			{
				base.Body.BoltMove(m_target.X - 150, base.Body.Y, 0);
			}
			base.Body.FallFrom(base.Body.X, base.Body.Y, "", 0, 0, 1000);
			base.Body.ChangeDirection(m_target, 0);
			base.Body.PlayMovie("beatB", 0, 0);
			if (base.Body.Direction == -1)
			{
				base.Body.RangeAttacking(base.Body.X - 180, base.Body.X, "cry", 500, directDamage: false);
			}
			else
			{
				base.Body.RangeAttacking(base.Body.X, base.Body.X + 180, "cry", 500, directDamage: false);
			}
			base.Body.CallFuction(PunchBeatC, 1000);
		}
	}

	private void PunchBeatC()
	{
		m_target = FindUntargetPlayer();
		if (m_target != null)
		{
			if (base.Body.X > m_target.X)
			{
				base.Body.BoltMove(m_target.X + 150, base.Body.Y, 0);
			}
			else
			{
				base.Body.BoltMove(m_target.X - 150, base.Body.Y, 0);
			}
			base.Body.FallFrom(base.Body.X, base.Body.Y, "", 0, 0, 1000);
			base.Body.ChangeDirection(m_target, 0);
			base.Body.PlayMovie("beatC", 0, 0);
			if (base.Body.Direction == -1)
			{
				base.Body.RangeAttacking(base.Body.X - 180, base.Body.X, "cry", 500, directDamage: false);
			}
			else
			{
				base.Body.RangeAttacking(base.Body.X, base.Body.X + 180, "cry", 500, directDamage: false);
			}
			base.Body.CallFuction(PunchBeatD, 1000);
		}
	}

	private void PunchBeatD()
	{
		m_target = FindUntargetPlayer();
		if (m_target != null)
		{
			if (base.Body.X > m_target.X)
			{
				base.Body.BoltMove(m_target.X + 150, base.Body.Y, 0);
			}
			else
			{
				base.Body.BoltMove(m_target.X - 150, base.Body.Y, 0);
			}
			base.Body.FallFrom(base.Body.X, base.Body.Y, "", 0, 0, 1000);
			base.Body.ChangeDirection(m_target, 0);
			base.Body.PlayMovie("beatD", 0, 0);
			if (base.Body.Direction == -1)
			{
				base.Body.RangeAttacking(base.Body.X - 180, base.Body.X, "cry", 500, directDamage: false);
			}
			else
			{
				base.Body.RangeAttacking(base.Body.X, base.Body.X + 180, "cry", 500, directDamage: false);
			}
		}
	}

	private void RemoveEffect()
	{
		if (m_popcan != null)
		{
			base.Game.RemovePhysicalObj(m_popcan, sendToClient: true);
		}
		if ((bool)base.Body.Properties2)
		{
			base.Body.AddBlood(base.Body.MaxBlood / 100 * 35);
			SetState();
		}
	}

	private void MoveToTarget(LivingCallBack callback, int delay, int delaycallback)
	{
		base.Body.PlayMovie("walk", delay, 0);
		m_target = FindUntargetPlayer();
		if (m_target != null)
		{
			m_target.Properties1++;
			if (base.Body.X > m_target.X)
			{
				base.Body.BoltMove(m_target.X + 150, base.Body.Y, delay + 1000);
			}
			else
			{
				base.Body.BoltMove(m_target.X - 150, base.Body.Y, delay + 1000);
			}
			base.Body.ChangeDirection(m_target, delay + 500);
			base.Body.CallFuction(callback, delaycallback);
		}
	}

	private void PunchBeatE()
	{
		base.Body.BeatDirect(m_target, "beatE", 0, 1, 1);
		m_target.AddEffect(new ReduceStrengthEffect(2, 100), 1000);
		m_target.AddEffect(new SealEffect(2), 1000);
		m_target.AddEffect(new LockDirectionEffect(2), 1000);
	}

	public override void OnStopAttacking()
	{
		base.OnStopAttacking();
	}

	public override void OnAfterTakedBomb()
	{
		base.OnAfterTakedBomb();
		SetState();
	}

	private void SetState()
	{
		if (base.Body.Blood <= base.Body.MaxBlood / 100 * 35)
		{
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "cry", "cryB");
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "stand", "standC");
			base.Body.Properties2 = true;
		}
		else
		{
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "cry", "cry");
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "stand", "stand");
			base.Body.Properties2 = false;
		}
	}
}
