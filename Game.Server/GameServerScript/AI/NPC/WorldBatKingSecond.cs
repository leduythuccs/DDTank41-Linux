using System.Collections.Generic;
using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.NPC;

public class WorldBatKingSecond : ABrain
{
	private int m_attackTurn = 0;

	private PhysicalObj moive;

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
		if (m_attackTurn == 0)
		{
			AttackA(0, base.Game.Map.Info.ForegroundWidth + 1);
			m_attackTurn++;
		}
		else
		{
			AttackB(0, base.Game.Map.Info.ForegroundWidth + 1);
			m_attackTurn = 0;
		}
	}

	public override void OnStopAttacking()
	{
		base.OnStopAttacking();
	}

	private void AttackA(int fx, int tx)
	{
		Player player = base.Game.FindRandomPlayer();
		if (player != null)
		{
			base.Body.CurrentDamagePlus = 10f;
			base.Body.PlayMovie("beatA", 1700, 0);
			base.Body.RangeAttacking(fx, tx, "cry", 4000, null);
		}
	}

	private void AttackB(int fx, int tx)
	{
		Player player = base.Game.FindRandomPlayer();
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
			moive = ((PVEGame)base.Game).Createlayer(item.X, item.Y, "moive", "asset.game.zero.294b", "out", 1, 0);
		}
		base.Body.PlayMovie("in", 100, 0);
	}
}
