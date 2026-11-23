using Game.Logic;
using Game.Logic.AI;

namespace GameServerScript.AI.NPC;

public class ThirdTerrorBloomNpc : ABrain
{
	private bool m_canWin = false;

	public override void OnHeal(int blood)
	{
		base.OnHeal(blood);
		if (base.Body.Blood >= base.Body.MaxBlood && !m_canWin)
		{
			m_canWin = true;
			((PVEGame)base.Game).SendFreeFocus(base.Body.X, 400, 1, 0, 0);
			base.Body.Say("Năng lượng đã đầy, nhớ bám chắc vào tôi!", 0, 6000, 2000);
			base.Body.PlayMovie("die", 8000, 0);
			base.Body.PlayMovie("grow", 10800, 0);
			((PVEGame)base.Game).SendFreeFocus(base.Body.X, 400, 2, 10500, 0);
			((PVEGame)base.Game).TotalKillCount++;
		}
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
	}

	public override void OnStopAttacking()
	{
		base.OnStopAttacking();
	}
}
