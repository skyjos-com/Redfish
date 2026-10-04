using System;
using System.Net;
using SMBLibrary;
using SMBLibrary.Authentication.GSSAPI;
using SMBLibrary.Server;

namespace RedfishService
{
    public class RedfishSMBServer : SMBServer
    {
        public RedfishSMBServer(SMBShareCollection shares, GSSProvider securityProvider)
            : base(shares, securityProvider)
        {
        }

        public void Start(IPAddress serverAddress, SMBTransportType transport, int port)
        {
            if (port < 1 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port), "The server port must be between 1 and 65535.");
            }

            base.Start(serverAddress, transport, port, true, true, false, null);
        }
    }
}
