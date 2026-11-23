using System.Collections.Generic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.NPC;

public class WorldBatKing : ABrain
{
	private int m_attackTurn = 0;

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
		if (base.Body.State == 10)
		{
			KillAttack(0, base.Game.Map.Info.ForegroundWidth + 1);
		}
		else if (m_attackTurn == 0)
		{
			AttackA();
			m_attackTurn++;
		}
		else
		{
			AttackB(0, base.Game.Map.Info.ForegroundWidth + 1);
			m_attackTurn = 0;
		}
	}

	private void KillAttack(int fx, int tx)
	{
		base.Body.CurrentDamagePlus = 100f;
		base.Body.PlayMovie("beatB", 3000, 0);
		base.Body.RangeAttacking(fx, tx, "cry", 5000, null);
	}

	public override void OnStopAttacking()
	{
		base.OnStopAttacking();
	}

	private void AttackA()
	{
		int x = base.Game.Random.Next(173, 1439);
		int y = base.Game.Random.Next(150, 700);
		base.Body.MoveTo(x, y, "fly", 3000, "", 12, AllAttackA);
	}

	private void AllAttackA()
	{
		Player player = base.Game.FindRandomPlayer();
		base.Body.Direction = base.Game.FindlivingbyDir(base.Body);
		if (player != null)
		{
			base.Body.CurrentDamagePlus = 10f;
			base.Body.PlayMovie("beatA", 1700, 0);
			base.Body.RangeAttacking(0, base.Game.Map.Info.ForegroundWidth + 1, "cry", 4000, null);
		}
	}

	private void AttackB(int fx, int tx)
	{
		Player player = base.Game.FindRandomPlayer();
		base.Body.Direction = base.Game.FindlivingbyDir(base.Body);
		if (player != null)
		{
			base.Body.CurrentDamagePlus = 15f;
			base.Body.PlayMovie("beatB", 1900, 0);
			base.Body.RangeAttacking(fx, tx, "cry", 4000, null);
			base.Body.CallFuction(GoMovie, 4000);
		}
	}

	private void GoMovie()
	{
		List<Player> allFightPlayers = base.Game.GetAllFightPlayers();
		foreach (Player item in allFightPlayers)
		{
		}
		base.Body.CallFuction(ShowIn, 2000);
	}

	private void ShowIn()
	{
		base.Body.PlayMovie("in", 100, 0);
	}

	public override void OnKillPlayerSay()
	{
		base.OnKillPlayerSay();
	}
}
