using System.Collections.Generic;
using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.NPC;

public class ThirdHardKingSecond : ABrain
{
	private int m_attackTurn = 0;

	private int m_maxNpcCount = 1;

	private int npcID = 3206;

	private SimpleBoss friendBoss;

	private int BloodRecover = 6000;

	private List<PhysicalObj> phyStarList = new List<PhysicalObj>();

	private static string[] AllAttackChat = new string[4] { "Kiếm thần!!! Kiếm thần!!", "Hãy đỡ tuyệt chiêu kiếm thần!!", "Kiếm thần!! Trợ giúp!!", "Kiếm thần đây. Hãy đỡ!!!!" };

	private static string[] CallChat = new string[4] { "Vệ binh! <br/> bảo vệ! ! ", "Dũng sĩ bộ lạc đâu thể hiện đi!", "Đệ tử của ta đâu mau ra đây!!!", "Lính đâu ra giết bọn chúng." };

	private static string[] ShootCallChat = new string[4] { "Chạy à? Chạy nữa đi!!!", "Một phát chết 3 con gián.", "Trẻ trâu mà láo à?", "Lũ gà!!! Chết này." };

	private SimpleBoss m_living;

	public override void OnBeginSelfTurn()
	{
		base.OnBeginSelfTurn();
		if (friendBoss != null && friendBoss.IsLiving)
		{
			return;
		}
		List<Living> list = base.Game.FindAllTurnBossLiving();
		foreach (Living item in list)
		{
			if (item != base.Body)
			{
				friendBoss = item as SimpleBoss;
				break;
			}
		}
	}

	public override void OnBeginNewTurn()
	{
		base.OnBeginNewTurn();
		m_body.CurrentDamagePlus = 1f;
		m_body.CurrentShootMinus = 1f;
	}

	public override void OnCreated()
	{
		base.OnCreated();
	}

	public override void OnStartAttacking()
	{
		base.OnStartAttacking();
		base.Body.Direction = base.Game.FindlivingbyDir(base.Body);
		bool flag = false;
		int num = 0;
		foreach (Player allFightPlayer in base.Game.GetAllFightPlayers())
		{
			if (allFightPlayer.IsLiving && allFightPlayer.X > base.Body.X - 300 && allFightPlayer.X < base.Body.X + 300)
			{
				int num2 = (int)base.Body.Distance(allFightPlayer.X, allFightPlayer.Y);
				if (num2 > num)
				{
					num = num2;
				}
				flag = true;
			}
		}
		if (flag)
		{
			KillAttack(base.Body.X - 300, base.Body.X + 300);
		}
		else if (friendBoss != null && !friendBoss.IsLiving)
		{
			base.Body.PlayMovie("castA", 800, 0);
			base.Body.Say("Anh trai ơi đừng đầu hàng.<br/>Em sẽ giúp anh hồi sinh!!!", 0, 1000);
			((PVEGame)base.Game).SendObjectFocus(friendBoss, 1, 3000, 1000);
			base.Body.CallFuction(ReviveFriendBoss, 5000);
		}
		else if (friendBoss != null && friendBoss.Blood <= friendBoss.NpcInfo.Blood / 10)
		{
			base.Body.PlayMovie("castA", 800, 0);
			base.Body.Say("Các ngươi làm sao đủ khả năng đánh bại anh ta!!!", 0, 1000);
			((PVEGame)base.Game).SendObjectFocus(friendBoss, 1, 3500, 5000);
			base.Body.CallFuction(RecoverFriendBlood, 4000);
		}
		else
		{
			JumpBeat();
		}
	}

	public void RecoverFriendBlood()
	{
		friendBoss.AddBlood(BloodRecover);
	}

	public void ReviveFriendBoss()
	{
		SimpleBoss simpleBoss = null;
		LivingConfig livingConfig = ((PVEGame)base.Game).BaseLivingConfig();
		livingConfig.ReduceBloodStart = 2;
		base.Game.RemoveLiving(friendBoss.Id);
		simpleBoss = ((PVEGame)base.Game).CreateBoss(friendBoss.NpcInfo.ID, friendBoss.X, friendBoss.Y, friendBoss.Direction, 1, "raise", livingConfig);
		simpleBoss.SetRelateDemagemRect(simpleBoss.NpcInfo.X, simpleBoss.NpcInfo.Y, simpleBoss.NpcInfo.Width, simpleBoss.NpcInfo.Height);
		simpleBoss.Say("Ta đã hồi sinh!!! Các ngươi phải chết.", 0, 3000);
		friendBoss = simpleBoss;
	}

	private void JumpBeat()
	{
		int num = base.Game.Random.Next(1, 100);
		base.Body.PlayMovie("walk", 1000, 0);
		if (base.Body.Y >= 310 && base.Body.Y <= 420)
		{
			if (num <= 50)
			{
				base.Body.FallFromTo(base.Body.X, 483, "", 1600, 0, 30, GoInBeat);
			}
			else
			{
				base.Body.FallFromTo(base.Body.X, 623, "", 1600, 0, 25, GoInBeat);
			}
		}
		else if (base.Body.Y >= 450 && base.Body.Y <= 540)
		{
			if (num <= 50)
			{
				base.Body.FallFromTo(base.Body.X, 623, "", 1600, 0, 25, GoInBeat);
			}
			else
			{
				base.Body.JumpTo(base.Body.X, 360, "", 1600, 0, 25, GoInBeat);
			}
		}
		else if (base.Body.Y >= 580 && base.Body.Y <= 668)
		{
			if (num <= 50)
			{
				base.Body.JumpTo(base.Body.X, 360, "", 1600, 0, 30, GoInBeat);
			}
			else
			{
				base.Body.JumpTo(base.Body.X, 483, "", 1600, 0, 25, GoInBeat);
			}
		}
	}

	private void GoInBeat()
	{
		switch (m_attackTurn)
		{
		case 0:
			ShootBeat();
			break;
		case 1:
			CreateAllAttack();
			break;
		case 2:
			CallbBoss();
			m_attackTurn = 0;
			break;
		}
		m_attackTurn++;
	}

	private void ShootBeat()
	{
		Player player = base.Game.FindRandomPlayer();
		base.Body.PlayMovie("stand", 0, 0);
		base.Body.CurrentDamagePlus = 1.8f;
		int num = base.Game.Random.Next(0, ShootCallChat.Length);
		base.Body.Say(ShootCallChat[num], 0, 1000);
		if (player != null && base.Body.ShootPoint(player.X, player.Y, 54, 1000, 10000, 3, 2f, 2300))
		{
			base.Body.PlayMovie("beatA", 1500, 0);
		}
	}

	private void CallbBoss()
	{
		int num = base.Game.Random.Next(0, CallChat.Length);
		base.Body.Say(CallChat[num], 0, 1000);
		base.Body.PlayMovie("castA", 1000, 0);
		base.Body.CallFuction(CreateChild, 4000);
	}

	public void CreateChild()
	{
		for (int i = 0; i < m_maxNpcCount; i++)
		{
			LivingConfig livingConfig = ((PVEGame)base.Game).BaseLivingConfig();
			livingConfig.CanCountKill = false;
			((PVEGame)base.Game).CreateNpc(npcID, 1369, 242, 0, -1, livingConfig);
		}
	}

	private void CreateAllAttack()
	{
		base.Body.CurrentDamagePlus = 1.5f;
		int num = base.Game.Random.Next(0, AllAttackChat.Length);
		base.Body.Say(AllAttackChat[num], 0, 1000);
		base.Body.PlayMovie("castA", 1000, 0);
		((PVEGame)base.Game).SendFreeFocus(827, 534, 1, 3000, 1000);
		((PVEGame)base.Game).SendFreeFocus(827, 534, 2, 3500, 1000);
		base.Body.CallFuction(CreateStarMovie, 5000);
	}

	public void CreateStarMovie()
	{
		foreach (Player allLivingPlayer in base.Game.GetAllLivingPlayers())
		{
			if (allLivingPlayer.IsLiving)
			{
				PhysicalObj item = ((PVEGame)base.Game).Createlayer(allLivingPlayer.X, allLivingPlayer.Y, "", "game.crazytank.assetmap.Dici", "", 1, 0);
				phyStarList.Add(item);
			}
		}
		base.Body.RangeAttacking(-10000, 10000, "cry", 700, null);
		base.Body.CallFuction(RemoveMovie, 1500);
	}

	public void RemoveMovie()
	{
		((PVEGame)base.Game).SendFreeFocus(827, 534, 1, 1000, 1000);
		foreach (PhysicalObj phyStar in phyStarList)
		{
			base.Game.RemovePhysicalObj(phyStar, sendToClient: true);
		}
		phyStarList = new List<PhysicalObj>();
	}

	private void KillAttack(int fx, int tx)
	{
		base.Body.CurrentDamagePlus = 100f;
		base.Body.PlayMovie("call", 1000, 0);
		base.Body.RangeAttacking(fx, tx, "cry", 4000, null);
	}

	private void RemovePhysicalObj()
	{
		m_living = base.Body as SimpleBoss;
		((PVEGame)base.Game).SendObjectFocus(m_living, 1, 0, 0);
		PhysicalObj[] array = base.Game.FindPhysicalObjByName("end");
		base.Game.RemovePhysicalObj(array[0], sendToClient: false);
	}

	private void CreateEffectEnd()
	{
		PhysicalObj physicalObj = ((PVEGame)base.Game).CreatePhysicalObj(m_living.X, m_living.Y, "end", "game.crazyTank.view.Focus", "", 1, 0);
		m_living.CallFuction(RemovePhysicalObj, 2000);
	}

	public override void OnDie()
	{
		List<Living> list = base.Game.FindAllTurnBossLiving();
		if (list.Count == 1)
		{
			((PVEGame)base.Game).TotalKillCount = 1;
		}
		if (list.Count <= 0)
		{
			((PVEGame)base.Game).TotalKillCount = 1;
			m_living = friendBoss;
			((PVEGame)base.Game).SendObjectFocus(m_living, 1, 5000, 0);
			m_living.CallFuction(CreateEffectEnd, 6000);
			m_living.CallFuction(CreateEffectEnd, 10000);
		}
	}

	public override void OnStopAttacking()
	{
		base.OnStopAttacking();
	}
}
