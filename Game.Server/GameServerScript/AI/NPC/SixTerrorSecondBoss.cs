using System.Collections.Generic;
using System.Linq;
using Game.Logic;
using Game.Logic.AI;
using Game.Logic.Phy.Object;

namespace GameServerScript.AI.NPC;

public class SixTerrorSecondBoss : ABrain
{
	private int m_redNpc = 6321;

	private int m_blueNpc = 6322;

	private SimpleNpc[] m_blue;

	private SimpleNpc[] m_red;

	private int m_bloodBuff = 3600;

	private bool m_affectAll = false;

	private List<PhysicalObj> m_phy = new List<PhysicalObj>();

	private SimpleNpc m_randomNpc;

	private SimpleNpc FindRandom(List<SimpleNpc> npcs)
	{
		SimpleNpc result = null;
		int index = base.Game.Random.Next(0, npcs.Count);
		if (npcs.Count > 0)
		{
			result = npcs[index];
		}
		return result;
	}

	private List<SimpleNpc> GetNpcList(SimpleNpc[] npcs)
	{
		List<SimpleNpc> list = new List<SimpleNpc>();
		foreach (SimpleNpc simpleNpc in npcs)
		{
			if (!simpleNpc.Config.IsComplete)
			{
				list.Add(simpleNpc);
			}
		}
		return list;
	}

	public override void OnBeginSelfTurn()
	{
		base.OnBeginSelfTurn();
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
		if (m_blue == null || m_red == null)
		{
			m_blue = base.Game.GetNPCLivingWithID(m_blueNpc);
			m_red = base.Game.GetNPCLivingWithID(m_redNpc);
		}
		m_affectAll = false;
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		List<SimpleNpc> npcList = GetNpcList(m_blue);
		List<SimpleNpc> npcList2 = GetNpcList(m_red);
		List<SimpleNpc> list = npcList.OrderBy((SimpleNpc p) => p.Blood).ToList();
		List<SimpleNpc> list2 = npcList2.OrderByDescending((SimpleNpc p) => p.Blood).ToList();
		if (GetNpcList(m_blue) == null)
		{
			m_affectAll = true;
			base.Body.PlayMovie("beatB", 1000, 0);
			(base.Game as PVEGame).SendFreeFocus(1100, 650, 1, 2500, 3000);
			base.Body.CallFuction(DamageNpcEffect, 3500);
			return;
		}
		foreach (SimpleNpc item in list)
		{
			if (item.Blood <= item.MaxBlood / 100 * 60)
			{
				num2++;
			}
			num5 += item.Blood;
			if (item.Blood <= 1)
			{
				num6++;
			}
		}
		if (num6 >= 1)
		{
			base.Body.PlayMovie("beat", 1000, 0);
			(base.Game as PVEGame).SendFreeFocus(1100, 650, 1, 2500, 3000);
			for (int i = 0; i < num6; i++)
			{
				list[i].AddBlood(m_bloodBuff, 0, 4000);
				base.Body.CallFuction(RestoreSingleNpcEffect, list[i], 3500);
			}
			return;
		}
		foreach (SimpleNpc item2 in list2)
		{
			if (item2.Blood >= item2.MaxBlood / 100 * 60)
			{
				num++;
			}
			num3 += item2.Blood;
			if (item2.Blood <= 1)
			{
				num4++;
			}
		}
		if (num3 / 100 * 80 > num5)
		{
			m_randomNpc = list[0];
			m_affectAll = ((num >= num2) ? true : false);
			base.Body.PlayMovie("beat", 1000, 0);
			(base.Game as PVEGame).SendFreeFocus(1100, 650, 1, 2500, 3000);
			base.Body.CallFuction(RestoreBloodNpcEffect, 3500);
		}
		else
		{
			m_randomNpc = list2[0];
			m_affectAll = ((num2 <= num) ? true : false);
			base.Body.PlayMovie("beatB", 1000, 0);
			(base.Game as PVEGame).SendFreeFocus(1100, 650, 1, 2500, 3000);
			base.Body.CallFuction(DamageNpcEffect, 3500);
		}
	}

	private void RestoreSingleNpcEffect(Living living)
	{
		m_phy.Add((base.Game as PVEGame).Createlayer(living.X, living.Y, "front", "asset.game.six.qunjia", "", 1, 0));
	}

	private void DamageNpcEffect()
	{
		List<SimpleNpc> npcList = GetNpcList(m_red);
		if (m_affectAll)
		{
			foreach (SimpleNpc item in npcList)
			{
				m_phy.Add((base.Game as PVEGame).Createlayer(item.X, item.Y, "front", "asset.game.six.qunti", "", 1, 0));
			}
		}
		else
		{
			m_phy.Add((base.Game as PVEGame).Createlayer(m_randomNpc.X, m_randomNpc.Y, "front", "asset.game.six.qunti", "", 1, 0));
		}
		base.Body.CallFuction(DamageNpcBuff, 500);
	}

	private void DamageNpcBuff()
	{
		List<SimpleNpc> npcList = GetNpcList(m_red);
		if (m_affectAll)
		{
			foreach (SimpleNpc item in npcList)
			{
				base.Body.BeatDirect(item, "", 100, 1, 1);
			}
			return;
		}
		base.Body.BeatDirect(m_randomNpc, "", 100, 1, 1);
	}

	private void RestoreBloodNpcEffect()
	{
		List<SimpleNpc> npcList = GetNpcList(m_blue);
		if (m_affectAll)
		{
			foreach (SimpleNpc item in npcList)
			{
				m_phy.Add((base.Game as PVEGame).Createlayer(item.X, item.Y, "front", "asset.game.six.qunjia", "", 1, 0));
			}
		}
		else
		{
			if (m_randomNpc == null)
			{
				DamageNpcEffect();
			}
			m_phy.Add((base.Game as PVEGame).Createlayer(m_randomNpc.X, m_randomNpc.Y, "front", "asset.game.six.qunjia", "", 1, 0));
		}
		base.Body.CallFuction(RestoreBloodNpcBuff, 500);
	}

	private void RestoreBloodNpcBuff()
	{
		List<SimpleNpc> npcList = GetNpcList(m_blue);
		if (m_affectAll)
		{
			foreach (SimpleNpc item in npcList)
			{
				item.AddBlood(m_bloodBuff);
			}
			return;
		}
		m_randomNpc.AddBlood(m_bloodBuff);
	}

	public override void OnDie()
	{
		base.OnDie();
	}

	public override void OnStopAttacking()
	{
		base.OnStopAttacking();
	}
}
