using Game.Logic.AI;

namespace GameServerScript.AI.NPC;

public class WorldSimpleBat : ABrain
{
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
		int[] array = new int[2] { 1, -1 };
		int num = base.Game.Random.Next(50, 100);
		base.Body.MoveTo(base.Body.X + num * array[base.Game.Random.Next(0, 2)], base.Body.Y, "stand", 3000, "", 5);
	}

	public override void OnStopAttacking()
	{
		base.OnStopAttacking();
	}
}
