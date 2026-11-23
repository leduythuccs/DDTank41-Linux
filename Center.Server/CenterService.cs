using Center.Server.Statics;
using Game.Base.Packets;
using log4net;
using SqlDataProvider.Data;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Net;
using CoreWCF;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Center.Server
{
    public class CenterService : ICenterService
    {
        private static WebApplication host;

        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public int AASGetState()
        {
			try
			{
				return CenterServer.Instance.ASSState ? 1 : 0;
			}
			catch
			{
			}
			return 2;
        }

        public bool AASUpdateState(bool state)
        {
			try
			{
				return CenterServer.Instance.SendAAS(state);
			}
			catch
			{
			}
			return false;
        }

        public bool ActivePlayer(bool isActive)
        {
			try
			{
				if (isActive)
				{
					LogMgr.AddRegCount();
					return true;
				}
			}
			catch
			{
			}
			return false;
        }

        public bool CreatePlayer(int id, string name, string password, bool isFirst)
        {
			try
			{
				Player player = new Player
				{
					Id = id,
					Name = name,
					Password = password,
					IsFirst = isFirst
				};
				LoginMgr.CreatePlayer(player);
				return true;
			}
			catch
			{
			}
			return false;
        }

        public bool ChargeMoney(int userID, string chargeID)
        {
			ServerClient serverClient = LoginMgr.GetServerClient(userID);
			//Console.WriteLine(userID.ToString(), chargeID);
			//Console.WriteLine(serverClient);
			if (serverClient != null)
			{
				serverClient.SendChargeMoney(userID, chargeID);
				return true;
			}
			return false;
        }

        public int ExperienceRateUpdate(int serverId)
        {
			try
			{
				return CenterServer.Instance.RateUpdate(serverId);
			}
			catch
			{
			}
			return 2;
        }

        public int GetConfigState(int type)
        {
			try
			{
				switch (type)
				{
				case 1:
					return CenterServer.Instance.ASSState ? 1 : 0;
				case 2:
					return CenterServer.Instance.DailyAwardState ? 1 : 0;
				}
			}
			catch
			{
			}
			return 2;
        }

        public List<ServerData> GetServerList()
        {
			ServerInfo[] servers = ServerMgr.Servers;
			List<ServerData> list = new List<ServerData>();
			ServerInfo[] array = servers;
			ServerInfo[] array2 = array;
			foreach (ServerInfo info in array2)
			{
				ServerData item = new ServerData
				{
					Id = info.ID,
					Name = info.Name,
					Ip = info.IP,
					Port = info.Port,
					State = info.State,
					MustLevel = info.MustLevel,
					LowestLevel = info.LowestLevel,
					Online = info.Online
				};
				list.Add(item);
			}
			return list;
        }

        public bool KitoffUser(int playerID, string msg)
        {
			try
			{
				ServerClient serverClient = LoginMgr.GetServerClient(playerID);
				if (serverClient != null)
				{
					msg = (string.IsNullOrEmpty(msg) ? "You are kicking out by GM!" : msg);
					serverClient.SendKitoffUser(playerID, msg);
					LoginMgr.RemovePlayer(playerID);
					return true;
				}
			}
			catch
			{
			}
			return false;
        }

        public bool MailNotice(int playerID)
        {
			try
			{
				ServerClient serverClient = LoginMgr.GetServerClient(playerID);
				if (serverClient != null)
				{
					GSPacketIn pkg = new GSPacketIn(117);
					pkg.WriteInt(playerID);
					pkg.WriteInt(1);
					serverClient.SendTCP(pkg);
					return true;
				}
			}
			catch
			{
			}
			return false;
        }

        public int NoticeServerUpdate(int serverId, int type)
        {
			try
			{
				return CenterServer.Instance.NoticeServerUpdate(serverId, type);
			}
			catch
			{
			}
			return 2;
        }

        public bool Reload(string type)
        {
			try
			{
				return CenterServer.Instance.SendReload(type);
			}
			catch
			{
			}
			return false;
        }

        public bool ReLoadServerList()
        {
			return ServerMgr.ReLoadServerList();
        }

        /// <summary>
        /// Hosts the WCF service with CoreWCF (System.ServiceModel's ServiceHost does not exist on .NET 8).
        /// Endpoints match the old App.config: net.tcp://host:2009/ (used by Road/Tank.Request/GameAdmin)
        /// and basicHttp http://host:2008/CenterService/.
        /// appSettings: CenterServiceHost (default 127.0.0.1), CenterServiceTcpPort (2009), CenterServiceHttpPort (2008).
        /// </summary>
        public static bool Start()
        {
			try
			{
				string hostName = System.Configuration.ConfigurationManager.AppSettings["CenterServiceHost"];
				IPAddress ip = string.IsNullOrEmpty(hostName) ? IPAddress.Loopback : IPAddress.Parse(hostName);
				int tcpPort = ParsePort("CenterServiceTcpPort", 2009);
				int httpPort = ParsePort("CenterServiceHttpPort", 2008);

				WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
				{
					ContentRootPath = AppContext.BaseDirectory
				});
				builder.Logging.ClearProviders();
				builder.WebHost.ConfigureKestrel(options => options.Listen(ip, httpPort));
				builder.WebHost.UseNetTcp(ip, tcpPort);
				builder.Services.AddServiceModelServices();

				WebApplication app = builder.Build();
				app.UseServiceModel(serviceBuilder =>
				{
					serviceBuilder.AddService<CenterService>();
					serviceBuilder.AddServiceEndpoint<CenterService, ICenterService>(new NetTcpBinding(SecurityMode.None), "/");
					serviceBuilder.AddServiceEndpoint<CenterService, ICenterService>(new BasicHttpBinding(), "/CenterService/");
				});
				app.StartAsync().GetAwaiter().GetResult();
				host = app;
				log.InfoFormat("Center Service started! net.tcp://{0}:{1}/ http://{0}:{2}/CenterService/", ip, tcpPort, httpPort);
				return true;
			}
			catch (Exception exception)
			{
				log.ErrorFormat("Start center server failed:{0}", exception);
				return false;
			}
        }

        private static int ParsePort(string key, int defaultPort)
        {
			string value = System.Configuration.ConfigurationManager.AppSettings[key];
			return int.TryParse(value, out int port) ? port : defaultPort;
        }

        public static void Stop()
        {
			try
			{
				if (host != null)
				{
					host.StopAsync().GetAwaiter().GetResult();
					((IDisposable)host).Dispose();
					host = null;
				}
			}
			catch
			{
			}
        }

        public bool SystemNotice(string msg)
        {
			try
			{
				CenterServer.Instance.SendSystemNotice(msg);
				return true;
			}
			catch
			{
				return false;
			}
        }

        public bool UpdateConfigState(int type, bool state)
        {
			try
			{
				return CenterServer.Instance.SendConfigState(type, state);
			}
			catch
			{
			}
			return false;
        }

        public bool ValidateLoginAndGetID(string name, string password, ref int userID, ref bool isFirst)
        {
			try
			{
				Player[] allPlayer = LoginMgr.GetAllPlayer();
				if (allPlayer != null)
				{
					Player[] array = allPlayer;
					Player[] array2 = array;
					foreach (Player player in array2)
					{
						if (player.Name == name && player.Password == password)
						{
							userID = player.Id;
							isFirst = player.IsFirst;
							return true;
						}
					}
				}
			}
			catch
			{
			}
			return false;
        }
    }
}
