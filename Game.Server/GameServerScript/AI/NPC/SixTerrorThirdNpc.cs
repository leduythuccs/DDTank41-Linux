using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.NPC;

public class SixTerrorThirdNpc : ABrain
{
	private int m_attackTurn = 0;

	private int m_x = 0;

	private int m_y = 0;

	private PhysicalObj m_popcan;

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

	private void SetState()
	{
		((PVEGame)base.Game).SendLivingActionMapping(base.Body, "standA", "stand");
		((PVEGame)base.Game).SendLivingActionMapping(base.Body, "standB", "stand");
		((PVEGame)base.Game).SendLivingActionMapping(base.Body, "cryA", "cry");
		((PVEGame)base.Game).SendLivingActionMapping(base.Body, "shield", "cry");
	}

	public override void OnStartAttacking()
	{
		base.OnStartAttacking();
		StopDirtyPlay();
	}

	private void CreateEffectQusan()
	{
		m_popcan = ((PVEGame)base.Game).Createlayer(m_x - 150, m_y + 120, "blue", "popcan_fla.qusan_8", "", 1, 0);
		base.Body.CallFuction(RemoveEffect, 1200);
	}

	private void RemoveEffect()
	{
		if (m_popcan != null)
		{
			base.Game.RemovePhysicalObj(m_popcan, sendToClient: true);
		}
	}

	private void StopDirtyPlay()
	{
		SimpleBoss[] array = base.Game.FindLivingTurnBossWithID(6331);
		foreach (SimpleBoss simpleBoss in array)
		{
			if (simpleBoss.Properties1 == 1)
			{
				base.Body.Say("Sao mặt đỏ thế? Dùng chất kích thích à!", 0, 0);
				base.Body.PlayMovie("beat", 1000, 0);
				((PVEGame)base.Game).SendGameFocus(simpleBoss, 3500, 0);
				m_x = simpleBoss.X;
				m_y = simpleBoss.Y;
				simpleBoss.Properties1 = 0;
				simpleBoss.IconPicture(eMirariType.Guard, result: false);
				simpleBoss.IconPicture(eMirariType.Damage, result: false);
				simpleBoss.Attack = simpleBoss.NpcInfo.Attack;
				simpleBoss.Defence = simpleBoss.NpcInfo.Defence;
				base.Body.CallFuction(CreateEffectQusan, 4000);
			}
		}
	}

	public override void OnStopAttacking()
	{
		base.OnStopAttacking();
	}

	public override void OnAfterTakeDamage(Living living)
	{
		if (!(living is Player))
		{
			return;
		}
		Player player = (Player)living;
		if (player.ShootCount <= 0)
		{
			base.Body.Properties2 = 1;
			base.Body.Properties1 = 1;
			base.Body.BlockTurn = false;
			base.Body.SyncAtTime = true;
			((PVEGame)base.Game).SendFreeFocus(base.Body.X, base.Body.Y - 100, 0, 3000, 0);
			switch (player.Properties1)
			{
			case 0:
				player.Properties1 = 1;
				base.Body.Say("<p class=\"red\">" + player.PlayerDetail.PlayerCharacter.NickName + "</p> đánh trọng tài là không được nha...", 0, 3500, 0);
				break;
			case 1:
				player.Properties1 = 2;
				base.Body.Say("<p class=\"red\">" + player.PlayerDetail.PlayerCharacter.NickName + "</p> đánh nữa ta đuổi cổ ra đó!", 0, 3500, 0);
				break;
			case 2:
				player.Properties1 = 3;
				base.Body.Say("<p class=\"red\">" + player.PlayerDetail.PlayerCharacter.NickName + "</p> chết này!", 0, 3500, 0);
				((PVEGame)base.Game).SendGameFocus(player, 4500, 0);
				player.Die(5000);
				break;
			}
		}
	}
}
