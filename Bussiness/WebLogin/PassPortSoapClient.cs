using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Bussiness.WebLogin
{
    [DebuggerStepThrough]
	[GeneratedCode("System.ServiceModel", "4.0.0.0")]
	public class PassPortSoapClient : ClientBase<PassPortSoap>, PassPortSoap
    {
        // .NET Core WCF clients cannot read <system.serviceModel> from App.config, so the
        // default endpoint is built in code. Override the address with appSettings["PassPortAddress"].
        public PassPortSoapClient()
			: base(CreateDefaultBinding(), CreateDefaultAddress())
        {
        }

        private static Binding CreateDefaultBinding()
        {
			return new BasicHttpBinding(BasicHttpSecurityMode.None);
        }

        private static EndpointAddress CreateDefaultAddress()
        {
			string address = System.Configuration.ConfigurationManager.AppSettings["PassPortAddress"];
			return new EndpointAddress(string.IsNullOrEmpty(address) ? "http://127.0.0.1/admingunny/Flash_Port/PassPort.asmx" : address);
        }




        public PassPortSoapClient(Binding binding, EndpointAddress remoteAddress)
			: base(binding, remoteAddress)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
		ChenckValidateResponse PassPortSoap.ChenckValidate(ChenckValidateRequest request)
        {
			return base.Channel.ChenckValidate(request);
        }

        public string ChenckValidate(string applicationname, string username, string password)
        {
			ChenckValidateRequest inValue = new ChenckValidateRequest();
			inValue.Body = new ChenckValidateRequestBody();
			inValue.Body.applicationname = applicationname;
			inValue.Body.username = username;
			inValue.Body.password = password;
			return ((PassPortSoap)this).ChenckValidate(inValue).Body.ChenckValidateResult;
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
		Get_UserSexResponse PassPortSoap.Get_UserSex(Get_UserSexRequest request)
        {
			return base.Channel.Get_UserSex(request);
        }

        public bool? Get_UserSex(string applicationname, string username)
        {
			Get_UserSexRequest inValue = new Get_UserSexRequest();
			inValue.Body = new Get_UserSexRequestBody();
			inValue.Body.applicationname = applicationname;
			inValue.Body.username = username;
			return ((PassPortSoap)this).Get_UserSex(inValue).Body.Get_UserSexResult;
        }
    }
}
