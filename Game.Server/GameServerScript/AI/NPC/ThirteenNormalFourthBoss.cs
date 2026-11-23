using System.Collections.Generic;
using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.NPC;

public class ThirteenNormalFourthBoss : ABrain
{
	private int m_attackTurn = 0;

	private int m_lastBloodFriend = 0;

	private SimpleBoss friendBoss;

	protected Player m_targer;

	private List<PhysicalObj> phyObjects = new List<PhysicalObj>();

	private PhysicalObj phyLineDead;

	private int m_friendBoss = 13109;

	private string[] Say_OpenLineDead = new string[3] { "Bọn mi còn nhớ ta không?", "Còn nhớ ta không? Ta trở về từ cõi chết đây!", "Lâu ngày không gặp, nhớ ta chứ?" };

	private string[] Say_DamageSingle = new string[5] { "Liệu có đỡ nổi không đây?", "Nếm thử cái này đi!", "Trước kia ở Vương Quốc Gà, các ngươi đã đánh bại bọn ta. Giờ thì đừng hòng nữa!", "Ngươi sẽ là mục tiêu của ta", "Liệu ngươi đỡ được không đây?" };

	private string[] Say_DamageGlobal = new string[4] { "Lũ khốn các ngươi phải chết hết", "Đừng mơ thắng được bọn ta", "Ngươi nghĩ ngươi đánh bại được ta à?", "Ta sẽ giết tất cả bọn khốn các ngươi" };

	private string[] Say_Angry = new string[4] { "Ta chịu hết nổi rồi", "Ta nóng rồi đây!", "Ta chết các ngươi cũng phải chết!", "Đùa đủ rồi. Xem đây!" };

	public override void OnBeginSelfTurn()
	{
		base.OnBeginSelfTurn();
		if (friendBoss == null)
		{
			friendBoss = base.Game.FindBossWithID(m_friendBoss);
		}
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
	}

	public override void OnStartAttacking()
	{
		base.OnStartAttacking();
		switch (m_attackTurn)
		{
		case 0:
			CreateLineDead();
			break;
		case 1:
			DamageDeadLine();
			break;
		case 2:
			SingleAttack();
			break;
		case 3:
			GlobalAttack();
			break;
		case 4:
			ChangeFireStatus();
			break;
		case 5:
			if (base.Body.Properties1 == 0 && friendBoss.Properties1 == 0)
			{
				CreateLineDead();
			}
			else
			{
				WishDead();
			}
			m_attackTurn = 0;
			break;
		}
		m_attackTurn++;
	}

	private void EffectFriendDead()
	{
		int num = m_lastBloodFriend + m_lastBloodFriend / 100 * 10;
		if (num >= base.Body.Blood)
		{
			base.Body.Say("Ta sẽ không tha thứ cho các ngươi đâu", 0, 1000);
			base.Body.PlayMovie("die", 2000, 0);
			base.Body.Die(5000);
		}
		else
		{
			base.Body.AddBlood(-num, 1);
			base.Body.Say("Các ngươi nghĩ như vậy là đủ hạ gục ta à?", 0, 1000);
			base.Body.PlayMovie("angry", 1000, 0);
			base.Body.PlayMovie("beatC", 3000, 5000);
			base.Body.CallFuction(DeadAllPlayers, 6000);
		}
	}

	private void WishDead()
	{
		if (base.Body.Properties1 == 1)
		{
			base.Body.Say("Hãy vĩnh biệt cuộc đời này đi lũ cờ hó.", 0, 2000);
			base.Body.PlayMovie("beatC", 2000, 5000);
			base.Body.CallFuction(DeadAllPlayers, 5500);
			base.Body.Die(5000);
		}
		else
		{
			(base.Game as PVEGame).SendObjectFocus(friendBoss, 1, 0, 0);
			friendBoss.Say("Hãy vĩnh biệt cuộc đời này đi lũ cờ hó.", 0, 2000);
			friendBoss.PlayMovie("beatC", 2000, 5000);
			friendBoss.CallFuction(DeadAllPlayers, 5500);
			friendBoss.Die(5000);
		}
	}

	private void DeadAllPlayers()
	{
		foreach (Player allLivingPlayer in base.Game.GetAllLivingPlayers())
		{
			allLivingPlayer.SyncAtTime = true;
			allLivingPlayer.Die();
		}
	}

	private void ChangeFireStatus()
	{
		base.Body.Properties1 = 1;
		int num = base.Game.Random.Next(Say_Angry.Length);
		base.Body.Say(Say_Angry[num], 0, 2000);
		base.Body.PlayMovie("angry", 2000, 0);
		base.Body.Config.CanTakeDamage = false;
		base.Body.SetRectBomb((base.Body as SimpleBoss).NpcInfo.X, (base.Body as SimpleBoss).NpcInfo.Y, (base.Body as SimpleBoss).NpcInfo.Width, (base.Body as SimpleBoss).NpcInfo.Height);
		base.Body.SetRelateDemagemRect(-20, -210, 40, 20);
	}

	private void SingleAttack()
	{
		int num = base.Game.Random.Next(Say_DamageSingle.Length);
		base.Body.Say(Say_DamageSingle[num], 0, 2000);
		base.Body.PlayMovie("cast", 2000, 0);
		m_targer = base.Game.FindRandomPlayer();
		if (m_targer != null)
		{
			(base.Game as PVEGame).SendObjectFocus(m_targer, 1, 3000, 0);
			base.Body.CallFuction(EffectSingle, 3200);
			base.Body.BeatDirect(m_targer, "", 3000, 1, 1);
		}
	}

	private void EffectSingle()
	{
		phyObjects.Add((base.Game as PVEGame).Createlayer(m_targer.X, m_targer.Y, "", "asset.game.ten.danbao", "", 1, 0));
	}

	private void EffectGlobal()
	{
		foreach (Player allLivingPlayer in base.Game.GetAllLivingPlayers())
		{
			phyObjects.Add((base.Game as PVEGame).Createlayer(allLivingPlayer.X, allLivingPlayer.Y, "", "asset.game.ten.qunbao", "", 1, 0));
		}
	}

	private void GlobalAttack()
	{
		base.Body.CurrentDamagePlus = 1.5f;
		int num = base.Game.Random.Next(Say_DamageGlobal.Length);
		base.Body.Say(Say_DamageGlobal[num], 0, 2000);
		base.Body.PlayMovie("cast", 2000, 0);
		m_targer = base.Game.FindRandomPlayer();
		if (m_targer != null)
		{
			(base.Game as PVEGame).SendObjectFocus(m_targer, 1, 3000, 0);
			base.Body.CallFuction(EffectGlobal, 3200);
			base.Body.RangeAttacking(base.Body.X - 10000, base.Body.Y + 10000, "cry", 4000, directDamage: false);
		}
	}

	private void DamageDeadLine()
	{
		base.Body.CurrentDamagePlus = 10f;
		base.Body.PlayMovie("beatD", 2000, 2000);
		base.Body.CallFuction(RemoveDeadLine, 2800);
		base.Body.RangeAttacking(base.Body.X, 1380, "cry", 3500, directDamage: true);
	}

	private void RemoveDeadLine()
	{
		base.Game.RemovePhysicalObj(phyLineDead, sendToClient: true);
		phyLineDead = null;
	}

	private void CreateLineDead()
	{
		int num = base.Game.Random.Next(Say_OpenLineDead.Length);
		base.Body.Say(Say_OpenLineDead[num], 0, 2000);
		base.Body.PlayMovie("beatA", 2100, 4000);
		base.Body.CallFuction(CreateLineDeadObject, 3500);
	}

	private void CreateLineDeadObject()
	{
		phyLineDead = (base.Game as PVEGame).Createlayer(783, 1020, "deadline", "asset.game.ten.tedabiaoji", "", 1, 0);
	}

	public override void OnDie()
	{
		base.OnDie();
	}

	public override void OnStopAttacking()
	{
		base.OnStopAttacking();
	}

	public override void OnAfterTakedFrozen()
	{
		base.OnAfterTakedFrozen();
		if (base.Body.Properties1 == 1)
		{
			base.Body.Properties1 = 0;
			base.Body.PlayMovie("stand", 100, 2000);
			base.Body.SetRelateDemagemRect((base.Body as SimpleBoss).NpcInfo.X, (base.Body as SimpleBoss).NpcInfo.Y, (base.Body as SimpleBoss).NpcInfo.Width, (base.Body as SimpleBoss).NpcInfo.Height);
			base.Body.Config.CanTakeDamage = true;
		}
	}
}
