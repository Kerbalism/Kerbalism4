using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public static class EVA
	{
		// return true if the vessel is a kerbal eva, and is flagged as dead
		public static bool IsDead(Vessel v)
		{
			if (!v.isEVA) return false;
			return DB.Kerbal(Lib.CrewList(v)[0].name).eva_dead;
		}
	}


} // KERBALISM
