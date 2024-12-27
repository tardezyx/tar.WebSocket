using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace tar.WebSocket {
  public class WebSocketClientOptions {
    public X509CertificateCollection ClientCertificates { get; set; }
    public CookieContainer Cookies { get; set; }
    public ICredentials Credentials { get; set; }
    public IWebProxy Proxy { get; set; }
    public TimeSpan KeepAliveInterval { get; set; }
    public bool? UseDefaultCredentials { get; set; }
  }
}