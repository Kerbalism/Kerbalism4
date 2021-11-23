﻿using Expansions.Serenity.DeployedScience.Runtime;

namespace KERBALISM
{
	public class CommHandlerCommNetSerenity : CommHandlerCommNetBase
	{
		private DeployedScienceCluster cluster;

		protected override void UpdateInputs(ConnectionInfo connection)
		{
			connection.transmitting = vd.vesselComms.transmittedFiles.Count > 0;
			connection.storm = vd.EnvStorm;

			if (cluster == null)
				cluster = Serenity.GetScienceCluster(vd.Vessel);

			connection.ec = 0.0;
			connection.ec_idle = 0.0;

			if (cluster == null)
			{
				baseRate = 0.0;
				connection.powered = false;
				connection.hasActiveAntenna = false;
			}
			else
			{
				baseRate = Settings.DataRateSurfaceExperiment;
				connection.powered = cluster.IsPowered;
				connection.hasActiveAntenna = cluster.AntennaParts.Count > 0;
			}
		}
	}
}
