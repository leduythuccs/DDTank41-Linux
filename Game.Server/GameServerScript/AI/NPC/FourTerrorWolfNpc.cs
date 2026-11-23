using System;
using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.NPC;

public class FourTerrorWolfNpc : ABrain
{
	private Player _mTargetPlayer;

	private const int FriendlyNpcId = 4305;

	private SimpleBoss _friendBoss;

	private bool _openEye;

	public override void OnBeginNewTurn()
	{
		base.OnBeginNewTurn();
		base.Body.CurrentDamagePlus = 1f;
		base.Body.CurrentShootMinus = 1f;
		base.Body.Config.HaveShield = _openEye;
		SetState();
	}

	public override void OnCreated()
	{
		base.OnCreated();
		if (_friendBoss == null)
		{
			SimpleBoss[] array = base.Game.FindLivingTurnBossWithID(4305);
			int num = 0;
			if (num < array.Length)
			{
				SimpleBoss friendBoss = array[num];
				_friendBoss = friendBoss;
			}
		}
		SetState();
	}

	public override void OnStartAttacking()
	{
		base.OnStartAttacking();
		base.Game.ShowBloodItem(base.Body.Id);
		switch (_friendBoss.Properties1)
		{
		case 0:
			if (!_openEye)
			{
				break;
			}
			base.Body.PlayMovie("BtoA", 1000, 4000);
			base.Body.CallFuction(RemoveTargetPlayer, 5000);
			_openEye = false;
			SetState();
			return;
		case 1:
			if (_openEye)
			{
				break;
			}
			base.Body.PlayMovie("AtoB", 2000, 4000);
			base.Body.CallFuction(FindRandomPlayer, 4000);
			_openEye = true;
			SetState();
			return;
		}
		if (_openEye && _mTargetPlayer != null)
		{
			int num = base.Game.Random.Next(100);
			if (num < 50 && base.Body.Distance(_mTargetPlayer.X, _mTargetPlayer.Y) > 600.0)
			{
				JumpToTargetPlayer();
			}
			else
			{
				RunToTargetPlayer();
			}
		}
		else if (!_openEye)
		{
			RandomMove();
		}
		else
		{
			Console.WriteLine("eye: " + _openEye + " - friendBoss.Properties1: " + _friendBoss.Properties1);
		}
	}

	private void RandomMove()
	{
		base.Body.ChangeDirection(-1, 1200);
		base.Body.ChangeDirection(1, 1800);
		base.Body.ChangeDirection(-1, 2300);
		int num = base.Game.Random.Next(-300, 300);
		int delayCallback = base.Game.GetDelayDistance(base.Body.X, base.Body.X + num, 3) + 800;
		base.Body.MoveTo(base.Body.X + num, base.Body.Y, "walkA", ((SimpleBoss)base.Body).NpcInfo.speed, "", 3500, NothingAction, delayCallback);
	}

	private void NothingAction()
	{
		base.Body.ChangeDirection(1, 500);
		base.Body.ChangeDirection(-1, 1000);
		base.Body.PlayMovie("beatA", 1000, 2000);
	}

	private void FindRandomPlayer()
	{
		_mTargetPlayer = base.Game.FindRandomPlayer();
		if (_mTargetPlayer != null)
		{
			base.Body.ChangeDirection(_mTargetPlayer, 500);
			((PVEGame)base.Game).SendPlayersPicture(_mTargetPlayer, 7, state: true);
			base.Body.Say("Lo chạy đi <p class=\"red\">" + _mTargetPlayer.PlayerDetail.PlayerCharacter.NickName + "</p>Ta đến đây...", 0, 2000, 0);
		}
	}

	private void RemoveTargetPlayer()
	{
		if (_mTargetPlayer != null)
		{
			((PVEGame)base.Game).SendPlayersPicture(_mTargetPlayer, 7, state: false);
			_mTargetPlayer = null;
		}
	}

	private void RunToTargetPlayer()
	{
		base.Body.ChangeDirection(_mTargetPlayer, 200);
		double num = base.Body.Distance(_mTargetPlayer.X, _mTargetPlayer.Y);
		bool flag = base.Body.FindDirection(_mTargetPlayer) == -1;
		int num2 = _mTargetPlayer.X + (flag ? 100 : (-100));
		int num3 = 0;
		if (num > 600.0)
		{
			num3 = base.Game.GetDelayDistance(base.Body.X, num2, 10) + 800;
			base.Body.MoveTo(num2, _mTargetPlayer.Y, "walkB", 10, "", 2200, BeatTargetPlayer, num3);
		}
		else
		{
			num3 = base.Game.GetDelayDistance(base.Body.X, num2, 5) + 800;
			base.Body.MoveTo(num2, _mTargetPlayer.Y, "walkC", 5, "", 2200, BeatTargetPlayer, num3);
		}
	}

	private void JumpToTargetPlayer()
	{
		base.Body.ChangeDirection(_mTargetPlayer, 200);
		base.Body.PlayMovie("jump", 2000, 0);
		((PVEGame)base.Game).SendObjectFocus(_mTargetPlayer, 1, 5000, 0);
		base.Body.BoltMove(_mTargetPlayer.X, _mTargetPlayer.Y, 6000);
		base.Body.PlayMovie("fall", 7000, 0);
		base.Body.RangeAttacking(_mTargetPlayer.X - 100, _mTargetPlayer.X + 100, "cry", 8000, null);
		base.Body.CallFuction(BeatTargetPlayer, 8500);
	}

	private void BeatTargetPlayer()
	{
		base.Body.ChangeDirection(_mTargetPlayer, 200);
		base.Body.CurrentDamagePlus = 2f;
		base.Body.Beat(_mTargetPlayer, "beatB", 0, 0, 1000);
	}

	public override void OnDie()
	{
		base.OnDie();
	}

	private void SetState()
	{
		if (_openEye)
		{
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "stand", "standB");
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "cry", "shield");
		}
		else
		{
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "stand", "standA");
			((PVEGame)base.Game).SendLivingActionMapping(base.Body, "cry", "cry");
		}
	}
}
