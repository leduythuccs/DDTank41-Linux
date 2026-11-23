using System.Collections.Generic;
using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Effects;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.NPC;

public class ThirteenNormalThirdBoss : ABrain
{
	private int m_attackTurn = 0;

	private SimpleBoss m_friendNpc;

	protected Player m_targer;

	private List<Player> m_listtarget;

	private List<PhysicalObj> phyObjects = new List<PhysicalObj>();

	private PhysicalObj phyBig1000;

	private PhysicalObj phyIronFire;

	private int m_friendBoss = 13106;

	private int m_guardDamage = 40;

	private int m_bloodHeal = 5000;

	private string[] Chat_CallFriendAttacking = new string[4] { "Ngươi sẽ bị đè bẹp như thịt viên!", "Tự nhiên thèm thịt viên quá ta!", "Ngươi có muốn thành tờ giấy mỏng không?!", "Ái chà! Con mồi của ta!" };

	private string[] Chat_CallChoiceTarget = new string[3] { "Không sai! Chính là ngươi!", "Người tiếp theo sẽ là ngươi", "Ngươi. Chính ngươi đấy!!" };

	private string[] Chat_CallFriendFucking = new string[5] { "Mi bị cái gì thế?", "Dior đang làm gì vậy!", "Ngươi muốn bị ta lột da hả???", "Này Quỷ khăn hồng, mi có để ý ta đang ở đó không vậy?", "Ngươi đang giúp bọn chúng hay giúp ta vậy?" };

	public override void OnBeginSelfTurn()
	{
		base.OnBeginSelfTurn();
		if (m_friendNpc == null)
		{
			SimpleBoss[] array = ((PVEGame)base.Game).FindLivingTurnBossWithID(m_friendBoss);
			if (array.Length != 0)
			{
				m_friendNpc = array[0];
			}
		}
		m_bloodHeal = base.Body.MaxBlood / 100 * 12;
	}

	public override void OnBeginNewTurn()
	{
		base.OnBeginNewTurn();
		m_body.CurrentDamagePlus = 1f;
		m_body.CurrentShootMinus = 1f;
		if (phyObjects == null || phyObjects.Count <= 0)
		{
			return;
		}
		foreach (PhysicalObj phyObject in phyObjects)
		{
			base.Game.RemovePhysicalObj(phyObject, sendToClient: true);
		}
		phyObjects.Clear();
	}

	public override void OnCreated()
	{
		base.OnCreated();
		base.Body.Properties1 = 0;
		base.Body.Config.CancelGuard = false;
	}

	public override void OnStartAttacking()
	{
		base.OnStartAttacking();
		switch (m_attackTurn)
		{
		case 0:
			FirstAttack();
			break;
		case 1:
			AttackToTarget();
			break;
		case 2:
			HealBloodOrWalkup();
			break;
		case 3:
			NextWalkAttack();
			break;
		case 4:
			HealBloodOrWalkup();
			m_attackTurn = -1;
			break;
		}
		m_attackTurn++;
	}

	private void FirstAttack()
	{
		if (base.Body.Properties1 != 0)
		{
			HealBloodOrWalkup();
		}
		else
		{
			FindRandomTarget();
		}
	}

	private void NextWalkAttack()
	{
		base.Body.CurrentDamagePlus = 1.8f;
		SimpleNpc[] array = base.Game.FindAllNpcLiving();
		if (array.Length == 0)
		{
			return;
		}
		SimpleNpc[] array2 = array;
		foreach (SimpleNpc obj in array2)
		{
			m_listtarget = new List<Player>();
			base.Body.ChangeDirection(obj, 1000);
			if (base.Body.FindDirection(obj) == 1)
			{
				foreach (Player allLivingPlayer in base.Game.GetAllLivingPlayers())
				{
					if (allLivingPlayer.X >= base.Body.X && allLivingPlayer.X <= base.Game.Map.Bound.Width)
					{
						m_listtarget.Add(allLivingPlayer);
					}
				}
				base.Body.MoveTo(base.Game.Map.Bound.Width, base.Body.Y, "walk", 30, "", 2000, CallDamageListPlayers, 2000);
				continue;
			}
			foreach (Player allLivingPlayer2 in base.Game.GetAllLivingPlayers())
			{
				if (allLivingPlayer2.X >= 0 && allLivingPlayer2.X <= base.Body.X)
				{
					m_listtarget.Add(allLivingPlayer2);
				}
			}
			base.Body.MoveTo(0, base.Body.Y, "walk", 30, "", 2000, CallDamageListPlayers, 2000);
		}
	}

	private void CallDamageListPlayers()
	{
		if (m_listtarget.Count > 0)
		{
			foreach (Player item in m_listtarget)
			{
				base.Body.BeatDirect(item, "", 1000, 1, 1);
			}
		}
		SimpleNpc[] array = base.Game.FindAllNpcLiving();
		if (array.Length != 0)
		{
			SimpleNpc[] array2 = array;
			foreach (SimpleNpc simpleNpc in array2)
			{
				if (simpleNpc.Properties1 >= 0)
				{
					Player player = base.Game.FindPlayer(simpleNpc.Properties1);
					if (player != null && player.IsLiving)
					{
						player.SetVisible(state: true);
						player.BlockTurn = false;
					}
				}
				simpleNpc.PlayMovie("die", 500, 0);
				simpleNpc.Die(2000);
			}
		}
		base.Body.CallFuction(RandomJumpBack, 2500);
	}

	private void RandomJumpBack()
	{
		int x = base.Game.Random.Next(100, base.Game.Map.Bound.Width - 100);
		base.Body.PlayMovie("jump", 1000, 0);
		base.Body.BoltMove(x, base.Body.Y, 3000);
		(base.Game as PVEGame).SendFreeFocus(x, base.Body.Y, 1, 2000, 0);
		base.Body.PlayMovie("fall", 3000, 2000);
	}

	private void HealBloodOrWalkup()
	{
		if (base.Body.Properties1 == 0)
		{
			base.Body.PlayMovie("beatA", 1000, 4000);
			base.Body.CallFuction(HealBlood, 3000);
		}
		else
		{
			(base.Game as PVEGame).SendLivingActionMapping(base.Body, "stand", "stand");
			(base.Game as PVEGame).SendLivingActionMapping(base.Body, "cry", "cry");
			base.Body.Properties1 = 0;
			base.Body.Config.CancelGuard = false;
			base.Body.PlayMovie("stand", 500, 0);
			base.Body.ChangeDirection(m_friendNpc, 1000);
			int num = base.Game.Random.Next(Chat_CallFriendFucking.Length);
			base.Body.Say(Chat_CallFriendFucking[num], 1, 2000, 3000);
		}
		base.Body.CallFuction(FindNextTarget, 5000);
	}

	private void FindNextTarget()
	{
		m_targer = base.Game.FindRandomPlayer();
		base.Body.ChangeDirection(m_targer, 500);
		int num = base.Game.Random.Next(Chat_CallChoiceTarget.Length);
		base.Body.Say(Chat_CallChoiceTarget[num], 1, 1000, 0);
		base.Body.CallFuction(SetTargetPlayer, 2000);
	}

	private void HealBlood()
	{
		base.Body.AddBlood(m_bloodHeal);
	}

	private void AttackToTarget()
	{
		base.Body.CurrentDamagePlus = 2.5f;
		base.Body.ChangeDirection(m_targer, 1000);
		base.Body.PlayMovie("jump", 2000, 1600);
		base.Body.BoltMove(m_targer.X, m_targer.Y, 3700);
		base.Body.PlayMovie("fallB", 4000, 2000);
		base.Body.BeatDirect(m_targer, "", 5000, 1, 1);
		base.Body.CallFuction(RemoveEffectPlayer, 6000);
	}

	private void RemoveEffectPlayer()
	{
		if (m_targer != null)
		{
			if (m_targer.EffectList.GetOfType(eEffectType.AddTargetEffect) is AddTargetEffect addTargetEffect)
			{
				addTargetEffect.Stop();
			}
			return;
		}
		foreach (Player allLivingPlayer in base.Game.GetAllLivingPlayers())
		{
			if (allLivingPlayer.EffectList.GetOfType(eEffectType.AddTargetEffect) is AddTargetEffect addTargetEffect2)
			{
				addTargetEffect2.Stop();
			}
		}
	}

	private void FindRandomTarget()
	{
		m_targer = base.Game.FindRandomPlayer();
		int num = base.Game.Random.Next(Chat_CallFriendAttacking.Length);
		base.Body.Say(Chat_CallFriendAttacking[num], 1, 1000, 0);
		base.Body.CallFuction(SetTargetPlayer, 3000);
	}

	private void SetTargetPlayer()
	{
		if (m_targer != null)
		{
			m_targer.AddEffect(new AddTargetEffect(), 0);
		}
	}

	public override void OnDie()
	{
		base.OnDie();
	}

	public override void OnStopAttacking()
	{
		base.OnStopAttacking();
	}
}
