using System.Collections.Generic;
using System.Drawing;
using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Effects;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.NPC;

public class SixHardFourBoss : ABrain
{
	private int m_attackTurn = 0;

	private SimpleNpc m_khien = null;

	private int m_khienID = 6244;

	private PhysicalObj m_redCircle;

	private PhysicalObj m_greenCircle;

	private PhysicalObj m_blueCircle;

	private Player m_target = null;

	private int m_targetDis = 0;

	private Point[] m_birth = new Point[3]
	{
		new Point(1280, 990),
		new Point(1835, 990),
		new Point(760, 990)
	};

	private List<Point> m_newBirth = new List<Point>();

	private List<Player> m_greenPlayers = new List<Player>();

	private List<Player> m_redPlayers = new List<Player>();

	private List<Player> m_bluePlayers = new List<Player>();

	private PhysicalObj m_effectTop = null;

	private static string[] m_turnChats = new string[2] { "Ngay cả Quốc Vương cũng không dám chọc giận Grace!", "Chết này!" };

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
		m_target = base.Game.FindRandomPlayer();
		if (base.Body.X > m_target.X)
		{
			m_targetDis = base.Body.X - m_target.X;
		}
		else
		{
			m_targetDis = m_target.X - base.Body.X;
		}
		if (m_attackTurn == 0)
		{
			ResetPlayersList();
			CreateCicrle();
			if (m_targetDis <= 450)
			{
				if (base.Body.X > m_target.X)
				{
					base.Body.MoveTo(m_target.X + 150, base.Body.X, "walk", 5, "", 1000, AttackFarPlayer, 3000);
				}
				else
				{
					base.Body.MoveTo(m_target.X - 150, base.Body.X, "walk", 5, "", 1000, AttackFarPlayer, 3000);
				}
			}
			else
			{
				ShootAttack();
			}
			SetState();
			m_attackTurn++;
		}
		else if (m_attackTurn == 1)
		{
			CreateEffectCircle();
			if (base.Body.X > m_target.X)
			{
				base.Body.MoveTo(base.Body.X - 50, base.Body.X, "walk", 5, "", 4000, DrinkPowerJuice, 2000);
			}
			else
			{
				base.Body.MoveTo(base.Body.X + 50, base.Body.X, "walk", 5, "", 4000, DrinkPowerJuice, 2000);
			}
			SetState();
			m_attackTurn++;
		}
		else if (m_attackTurn == 2)
		{
			ResetPlayersList();
			CreateCicrle();
			base.Body.CallFuction(BodyAddBlood, 1500);
			m_attackTurn++;
		}
		else if (m_attackTurn == 3)
		{
			CreateEffectCircle();
			base.Body.CallFuction(JumpAndAttack, 4000);
			m_attackTurn++;
		}
		else if (m_attackTurn == 4)
		{
			ResetPlayersList();
			if (base.Body.X > m_target.X)
			{
				base.Body.MoveTo(base.Body.X - 50, base.Body.X, "walk", 5, "", 0, CreateCicrle, 1000);
			}
			else
			{
				base.Body.MoveTo(base.Body.X + 50, base.Body.X, "walk", 5, "", 0, CreateCicrle, 1000);
			}
			ShootAttack();
			m_attackTurn++;
		}
		else if (m_attackTurn == 5)
		{
			CreateEffectCircle();
			base.Body.CallFuction(AllAttack, 4000);
			m_attackTurn++;
		}
		else if (m_attackTurn == 6)
		{
			ResetPlayersList();
			if (base.Body.X > m_target.X)
			{
				base.Body.MoveTo(base.Body.X - 50, base.Body.X, "walk", 5, "", 0, CreateCicrle, 1000);
			}
			else
			{
				base.Body.MoveTo(base.Body.X + 50, base.Body.X, "walk", 5, "", 0, CreateCicrle, 1000);
			}
			ShootAttack();
			m_attackTurn++;
		}
		else if (m_attackTurn == 7)
		{
			CreateEffectCircle();
			base.Body.CallFuction(ReadNewsPapperAttack, 4000);
			m_attackTurn = 0;
		}
		base.Body.CallFuction(SetState, 9000);
		base.Body.CallFuction(SetRect, 9000);
	}

	private void SetRect()
	{
		base.Body.SetRect(((SimpleBoss)base.Body).NpcInfo.X, ((SimpleBoss)base.Body).NpcInfo.Y, ((SimpleBoss)base.Body).NpcInfo.Width, ((SimpleBoss)base.Body).NpcInfo.Height);
		base.Body.FireY = -120;
		if (base.Body.Direction == -1)
		{
			base.Body.SetRelateDemagemRect(35, -60, 25, 80);
			base.Body.SetRect(((SimpleBoss)base.Body).NpcInfo.X, ((SimpleBoss)base.Body).NpcInfo.Y, ((SimpleBoss)base.Body).NpcInfo.Width, ((SimpleBoss)base.Body).NpcInfo.Height);
		}
		else
		{
			base.Body.SetRelateDemagemRect(-35, -60, 25, 80);
			base.Body.SetRect(-((SimpleBoss)base.Body).NpcInfo.X - ((SimpleBoss)base.Body).NpcInfo.Width, ((SimpleBoss)base.Body).NpcInfo.Y, ((SimpleBoss)base.Body).NpcInfo.Width, ((SimpleBoss)base.Body).NpcInfo.Height);
		}
	}

	private void ResetPlayersList()
	{
		m_bluePlayers = new List<Player>();
		m_redPlayers = new List<Player>();
		m_greenPlayers = new List<Player>();
	}

	private void ReadNewsPapperAttack()
	{
		base.Body.Say("Dạo này tin tức hot nhỉ.", 0, 0);
		base.Body.PlayMovie("beatC", 500, 0);
		if (base.Body.Direction == -1)
		{
			base.Body.RangeAttacking(base.Body.X - 10000, base.Body.X, "cry", 2500, directDamage: false);
		}
		else
		{
			base.Body.RangeAttacking(base.Body.X, base.Body.X + 10000, "cry", 2500, directDamage: false);
		}
	}

	private void AllAttack()
	{
		base.Body.Say("Đưa tôi 1 cái mic.", 0, 0);
		base.Body.CallFuction(CreateGlobalAttackEffect, 2500);
		base.Body.RangeAttacking(base.Body.X - 10000, base.Body.X + 10000, "cry", 9500, directDamage: false);
		base.Body.Say("Ôi! Sống rồi, lương tháng này có rồi.", 0, 9000);
		base.Body.PlayMovie("beatE", 9000, 800);
		base.Body.PlayMovie("standA", 10000, 0);
	}

	private void CreateGlobalAttackEffect()
	{
		m_effectTop = ((PVEGame)base.Game).CreateLayerTop(0, 0, "", "asset.game.six.chang", "", 1, 0);
		base.Body.CallFuction(RemoveGlobalAttackEffect, 9800);
	}

	private void RemoveGlobalAttackEffect()
	{
		if (m_effectTop != null)
		{
			base.Game.RemovePhysicalObj(m_effectTop, sendToClient: true);
		}
	}

	private void JumpAndAttack()
	{
		base.Body.Say("Đường đang thi công, phải đi đường vòng.", 0, 0);
		base.Body.PlayMovie("beatD", 1500, 0);
		base.Body.BoltMove(m_target.X, base.Body.Y, 3200);
		base.Body.RangeAttacking(base.Body.X - 200, base.Body.X + 200, "cry", 4500, directDamage: false);
	}

	private void AttackFarPlayer()
	{
		int num = base.Game.Random.Next(0, m_turnChats.Length);
		base.Body.ChangeDirection(m_target, 0);
		base.Body.Say(m_turnChats[num], 0, 0);
		base.Body.PlayMovie("beatB", 1000, 0);
		base.Body.RangeAttacking(m_target.X - 80, m_target.X + 80, "cry", 2500, directDamage: false);
	}

	private void ShootAttack()
	{
		base.Body.SetRect(-75, -120, 136, 182);
		List<Player> allLivingPlayers = base.Game.GetAllLivingPlayers();
		for (int i = 0; i < allLivingPlayers.Count; i++)
		{
			if (allLivingPlayers[i] != null)
			{
				int num = i * 1000 + i * 400;
				base.Body.ChangeDirection(allLivingPlayers[i], num);
				int x = base.Game.Random.Next(allLivingPlayers[i].X - 50, allLivingPlayers[i].X + 50);
				if (base.Body.ShootPoint(x, allLivingPlayers[i].Y, 61, 1000, 10000, 1, 1f, num + 2200))
				{
					base.Body.PlayMovie("beatA", num + 1700, 0);
				}
			}
		}
	}

	private void DrinkPowerJuice()
	{
		base.Body.ChangeDirection(m_target, 0);
		base.Body.Say("Chất kích thích à? Ta cũng muốn thử!", 0, 0);
		base.Body.PlayMovie("inX", 1000, 5300);
		base.Body.CallFuction(CreateChild, 5300);
	}

	private void CreateEffectCircle()
	{
		PhysicalObj physicalObj = null;
		PhysicalObj physicalObj2 = null;
		PhysicalObj physicalObj3 = null;
		PhysicalObj physicalObj4 = null;
		foreach (Player allFightPlayer in base.Game.GetAllFightPlayers())
		{
			physicalObj = ((PVEGame)base.Game).Createlayer(m_greenCircle.X, m_greenCircle.Y, "green", "asset.game.six.popcan", "green", 1, 0);
			physicalObj2 = ((PVEGame)base.Game).Createlayer(m_blueCircle.X, m_blueCircle.Y, "green", "asset.game.six.popcan", "blue", 1, 0);
			physicalObj3 = ((PVEGame)base.Game).Createlayer(m_redCircle.X, m_redCircle.Y, "green", "asset.game.six.popcan", "red", 1, 0);
			if (allFightPlayer.X > m_greenCircle.X - 120 && allFightPlayer.X < m_greenCircle.X + 120)
			{
				m_greenPlayers.Add(allFightPlayer);
			}
			if (allFightPlayer.X > m_blueCircle.X - 120 && allFightPlayer.X < m_blueCircle.X + 120)
			{
				m_bluePlayers.Add(allFightPlayer);
			}
			if (allFightPlayer.X > m_redCircle.X - 120 && allFightPlayer.X < m_redCircle.X + 120)
			{
				m_redPlayers.Add(allFightPlayer);
			}
			if (m_redPlayers.Count > 0)
			{
				base.Body.CallFuction(CreateRed, 1600);
				PlayerAddAttack(m_redPlayers);
			}
			if (m_bluePlayers.Count > 0)
			{
				base.Body.CallFuction(CreateBlue, 1600);
				PlayerAddArmor(m_bluePlayers);
			}
			if (m_greenPlayers.Count > 0)
			{
				PlayerAddBlood(m_greenPlayers);
			}
			base.Body.CallFuction(RemoveAllPhysicalObj, 3500);
		}
	}

	private void RemoveAllPhysicalObj()
	{
		List<PhysicalObj> allPhysicalObjSafe = base.Game.Map.GetAllPhysicalObjSafe();
		for (int i = 0; i < allPhysicalObjSafe.Count; i++)
		{
			base.Game.RemovePhysicalObj(allPhysicalObjSafe[i], sendToClient: true);
		}
	}

	private void PlayerAddBlood(List<Player> player)
	{
		foreach (Player item in player)
		{
			item.AddBlood(2500);
		}
	}

	private void CreateRed()
	{
		foreach (Player redPlayer in m_redPlayers)
		{
			PhysicalObj physicalObj = null;
			physicalObj = ((PVEGame)base.Game).Createlayer(redPlayer.X, redPlayer.Y, "attack", "asset.game.six.pup", "", 1, 0);
		}
	}

	private void CreateBlue()
	{
		foreach (Player bluePlayer in m_bluePlayers)
		{
			PhysicalObj physicalObj = null;
			physicalObj = ((PVEGame)base.Game).Createlayer(bluePlayer.X, bluePlayer.Y, "armor", "asset.game.six.aup", "", 1, 0);
		}
	}

	private void PlayerAddAttack(List<Player> player)
	{
		foreach (Player item in player)
		{
			item.Attack += 250.0;
			item.AddEffect(new DamageEffect(2), 2000);
		}
	}

	private void PlayerAddArmor(List<Player> player)
	{
		foreach (Player item in player)
		{
			item.AddEffect(new AddGuardEquipEffect(2, 250, isArrmor: true), 2000);
		}
	}

	private void BodyAddBlood()
	{
		SimpleNpc[] array = base.Game.FindAllNpcWithXandY(base.Body.X, base.Body.Y);
		List<SimpleNpc> list = new List<SimpleNpc>();
		if (array != null)
		{
			list.Add(array[0]);
		}
		if (list.Count > 0)
		{
			base.Body.Say("Ôi! Đã quá...............", 0, 0);
			base.Body.PlayMovie("xixue", 0, 2000);
			list[0].SetHidden(state: true);
			list[0].BoltMove(0, 0, 0);
			list[0].Die(0);
			base.Game.RemoveLiving(list[0], sendToClient: true);
			base.Body.CallFuction(BloodBuff, 2000);
			list.RemoveAt(0);
		}
		else
		{
			ShootAttack();
		}
		base.Body.CallFuction(SetState, 1000);
	}

	private void BloodBuff()
	{
		base.Body.AddBlood(base.Body.MaxBlood / 2);
	}

	private void CreateChild()
	{
		LivingConfig livingConfig = ((PVEGame)base.Game).BaseLivingConfig();
		livingConfig.isShowBlood = true;
		livingConfig.isShowSmallMapPoint = false;
		livingConfig.IsTurn = false;
		m_khien = ((SimpleBoss)base.Body).CreateChild(m_khienID, base.Body.X, base.Body.Y, 0, base.Body.Direction, showBlood: true, livingConfig);
		m_khien.SetRelateDemagemRect(-75, -130, 160, 130);
		m_khien.SetRect(-75, -130, 160, 130);
		m_khien.Properties1 = 1;
		base.Body.Properties1 = 1;
		SetState();
	}

	private void SetState()
	{
		SimpleNpc[] array = base.Game.FindAllNpcWithXandY(base.Body.X, base.Body.Y);
		List<SimpleNpc> list = new List<SimpleNpc>();
		if (array != null)
		{
			list.Add(array[0]);
		}
		if (list.Count > 0)
		{
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "cry", "cryA");
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "stand", "standC");
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "shield", "shieldB");
			base.Body.SetRelateDemagemRect(0, 0, 0, 0);
			return;
		}
		((PVEGame)base.Game).SendLivingActionMapping(base.Body, "cry", "cryA");
		((PVEGame)base.Game).SendLivingActionMapping(base.Body, "stand", "standA");
		((PVEGame)base.Game).SendLivingActionMapping(base.Body, "shield", "shield");
		if (base.Body.Direction == -1)
		{
			base.Body.SetRelateDemagemRect(35, -60, 25, 80);
		}
		else
		{
			base.Body.SetRelateDemagemRect(-35, -60, 25, 80);
		}
		base.Body.FireX = -120;
	}

	private void CreateCicrle()
	{
		int[] array = new int[3] { 0, 1, 2 };
		base.Game.Shuffer(array);
		m_greenCircle = ((PVEGame)base.Game).Createlayer(m_birth[array[0]].X, m_birth[array[0]].Y, "green", "asset.game.six.greencircle", "", 1, 0);
		m_redCircle = ((PVEGame)base.Game).Createlayer(m_birth[array[1]].X, m_birth[array[1]].Y, "red", "asset.game.six.redcircle", "", 1, 0);
		m_blueCircle = ((PVEGame)base.Game).Createlayer(m_birth[array[2]].X, m_birth[array[2]].Y, "blue", "asset.game.six.bluecircle", "", 1, 0);
	}

	public override void OnStopAttacking()
	{
		base.OnStopAttacking();
	}

	public override void OnAfterTakedBomb()
	{
		base.OnAfterTakedBomb();
		if (m_khien != null)
		{
			base.Body.Properties1 = 1;
		}
		else
		{
			base.Body.Properties1 = 0;
		}
		SetState();
	}
}
