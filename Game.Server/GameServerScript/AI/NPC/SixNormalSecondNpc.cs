using System;
using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.NPC;

public class SixNormalSecondNpc : ABrain
{
	private bool m_startTurn = true;

	private const int m_cauthangphai = 1610;

	private const int m_cauthangtrai = 600;

	private int KhoangCach_Du = 0;

	private int m_khoangCachBu = 0;

	private int[] m_diemden = new int[10] { 675, 755, 835, 915, 995, 1075, 1155, 1235, 1315, 1395 };

	private bool InPos(int y1, int y2)
	{
		return y1 <= base.Body.Y && base.Body.Y <= y2;
	}

	private int GetKhoangCach()
	{
		double num = (double)base.Body.Blood * 1.0 / ((double)base.Body.MaxBlood * 1.0) * 100.0;
		int num2 = 1050;
		if (num < 10.0)
		{
			num2 = base.Game.Random.Next(500, 550);
		}
		else if (num < 30.0)
		{
			num2 = base.Game.Random.Next(550, 600);
		}
		else if (num < 50.0)
		{
			num2 = base.Game.Random.Next(650, 700);
		}
		else if (num < 70.0)
		{
			num2 = base.Game.Random.Next(700, 750);
		}
		else if (num < 90.0)
		{
			num2 = base.Game.Random.Next(800, 850);
		}
		return num2 - m_khoangCachBu * 20;
	}

	public override void OnCreated()
	{
		base.OnCreated();
	}

	public override void OnBeginSelfTurn()
	{
		base.OnBeginSelfTurn();
	}

	public override void OnBeginNewTurn()
	{
		base.OnBeginNewTurn();
		if (!base.Body.Config.IsComplete)
		{
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "focus", "focuscl");
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "fly", "walk");
			if (base.Body.Blood == 1)
			{
				((PVEGame)base.Game).SendLivingActionMapping(base.Body, "stand", "standB");
				((PVEGame)base.Game).SendLivingActionMapping(base.Body, "cry", "cryB");
				base.Body.PlayMovie("standB", 0, 0);
			}
			else
			{
				((PVEGame)base.Game).SendLivingActionMapping(base.Body, "stand", "stand");
				((PVEGame)base.Game).SendLivingActionMapping(base.Body, "cry", "cry");
				base.Body.PlayMovie("stand", 0, 0);
			}
		}
	}

	public override void OnStartAttacking()
	{
		if (m_startTurn)
		{
			BeginStart();
			m_startTurn = false;
			return;
		}
		KhoangCach_Du = 0;
		if (base.Body.Blood != 1)
		{
			if (InPos(980, 1000) || InPos(675, 732))
			{
				Moveto1();
			}
			else if (InPos(820, 890) || InPos(525, 600))
			{
				Moveto2();
			}
			else if (InPos(380, 415))
			{
				Moveto3();
			}
		}
	}

	public override void OnBeforeTakedDamage(Living source, ref int damageAmount, ref int criticalAmount)
	{
		base.OnBeforeTakedDamage(source, ref damageAmount, ref criticalAmount);
		if (base.Body.Blood == 1)
		{
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "stand", "standB");
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "cry", "cryB");
		}
		else
		{
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "stand", "stand");
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "cry", "cry");
		}
	}

	public override void OnStopAttacking()
	{
		base.OnStopAttacking();
	}

	private void BeginStart()
	{
		base.Body.CallFuction(SetHide, base.Body.Properties1 * 1000);
		base.Body.MoveTo(595, base.Body.Y, "walk", 7, "", base.Body.Properties1 * 1000, ClaimUp, 3500);
	}

	private void ClaimUp()
	{
		base.Body.MoveTo(base.Body.X, base.Body.Y - 100, "flyup", 7, "", 0, SetPosition, 1000);
	}

	private void SetPosition()
	{
		base.Body.MoveTo((int)base.Body.Properties3, base.Body.Y, "flyLR", 7, "", 1000, SetState, 4000);
	}

	private void SetState()
	{
		base.Body.PlayMovie("stand", 0, 0);
	}

	private void SetHide()
	{
		base.Body.IsHide = false;
	}

	private void Moveto3()
	{
		int khoangCach = GetKhoangCach();
		int num = khoangCach + base.Body.X;
		if (num >= 1610)
		{
			num = 1610;
			KhoangCach_Du = khoangCach + base.Body.X - 1610;
			KhoangCach_Du = ((KhoangCach_Du > 0) ? KhoangCach_Du : 0);
			m_khoangCachBu++;
		}
		int delayCallback = base.Game.GetDelayDistance(base.Body.X, num, 7) + 1000;
		base.Body.MoveTo(num, base.Body.Y, "flyLR", 7, "", 100, Moveto3A, delayCallback);
	}

	private void Moveto3A()
	{
		if (Math.Abs(base.Body.X - 1610) <= 2)
		{
			int delayCallback = base.Game.GetDelayDistance(base.Body.Y, base.Body.Y - 120, 7) + 1000;
			base.Body.MoveTo(base.Body.X, base.Body.Y - 120, "flyup", 7, "", 100, WIN, delayCallback);
		}
	}

	private void WIN()
	{
		((PVEGame)base.Game).SendLivingActionMapping(base.Body, "stand", "happy");
		int num = base.Game.GetDelayDistance(base.Body.X, m_diemden[base.Game.Top], 7) + 1000;
		base.Body.MoveTo(m_diemden[base.Game.Top], base.Body.Y, "fly", 100, "", 7);
		base.Body.SetRelateDemagemRect(0, 0, 0, 0);
		base.Game.Top++;
		base.Game.StopLiving = true;
		if (((SimpleNpc)base.Body).NpcInfo.ID == 6121)
		{
			((PVEGame)base.Game).TotalKillCount++;
		}
		else
		{
			((PVEGame)base.Game).Param2++;
		}
	}

	private void Moveto2()
	{
		int khoangCach = GetKhoangCach();
		int num = base.Body.X - khoangCach;
		if (num <= 600)
		{
			num = 600;
			KhoangCach_Du = khoangCach + 600 - base.Body.X;
			KhoangCach_Du = ((KhoangCach_Du > 0) ? KhoangCach_Du : 0);
			m_khoangCachBu++;
		}
		int delayCallback = base.Game.GetDelayDistance(base.Body.X, num, 7) + 1000;
		base.Body.MoveTo(num, base.Body.Y, "flyLR", 7, "", 100, Moveto2A, delayCallback);
	}

	private void Moveto2A()
	{
		if (Math.Abs(base.Body.X - 600) <= 2)
		{
			int delayCallback = base.Game.GetDelayDistance(base.Body.Y, base.Body.Y - 150, 7) + 1000;
			base.Body.MoveTo(base.Body.X, base.Body.Y - 150, "flyup", 7, "", 100, Moveto2B, delayCallback);
		}
	}

	private void Moveto2B()
	{
		if (Math.Abs(base.Body.X - 600) <= 2)
		{
			base.Body.Direction = 1;
			if (KhoangCach_Du == 0)
			{
				base.Body.MoveTo(base.Body.X + base.Game.Random.Next(40, 70), base.Body.Y, "flyLR", 100, "", 7);
			}
			else
			{
				base.Body.MoveTo(base.Body.X + KhoangCach_Du, base.Body.Y, "flyLR", 100, "", 7);
			}
		}
	}

	private void Moveto1()
	{
		int khoangCach = GetKhoangCach();
		int num = khoangCach + base.Body.X;
		if (num >= 1610)
		{
			num = 1610;
			KhoangCach_Du = khoangCach + base.Body.X - 1610;
			KhoangCach_Du = ((KhoangCach_Du > 0) ? KhoangCach_Du : 0);
			m_khoangCachBu++;
		}
		int delayCallback = base.Game.GetDelayDistance(base.Body.X, num, 7) + 1000;
		base.Body.MoveTo(num, base.Body.Y, "flyLR", 7, "", 100, Moveto1A, delayCallback);
	}

	private void Moveto1A()
	{
		if (Math.Abs(base.Body.X - 1610) <= 2)
		{
			int delayCallback = base.Game.GetDelayDistance(base.Body.Y, base.Body.Y - 150, 7) + 1000;
			base.Body.MoveTo(base.Body.X, base.Body.Y - 150, "flyup", 7, "", 100, Moveto1B, delayCallback);
		}
	}

	private void Moveto1B()
	{
		if (Math.Abs(base.Body.X - 1610) <= 2)
		{
			base.Body.Direction = -1;
			if (KhoangCach_Du == 0)
			{
				base.Body.MoveTo(base.Body.X - base.Game.Random.Next(40, 70), base.Body.Y, "flyLR", 100, "", 7);
			}
			else
			{
				base.Body.MoveTo(base.Body.X - KhoangCach_Du, base.Body.Y, "flyLR", 100, "", 7);
			}
		}
	}

	private void NULL()
	{
	}
}
