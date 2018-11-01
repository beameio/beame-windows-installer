# beame-windows-installer
The Windows Installer tools are set of commands to install all prerequisites such as NPM, OpenSSL and more before installing the main component

Currently they allow the installing of the Beame SDK and Beame Gatekeeper component on a windows 64 bits machine running .net framework 4.5 (tested on windows server 2012 and 2016)

## Application Settings - app.config

Values configured inside <appSettings> as <add key="OpenSSLPath" value="C:\OpenSSL-Win64"/>
All values are text fields

### Available settings
* GatekeeperName

    Name of the gatekeeper, will identify the gatekeeper in the mobile app and ui. e.g Beame Gatekeeper
* GatekeeperMode

    Type of gatekeeper to install. Can be Gatekeeper  or  CentralLogin

* ProxyAddressProtocol

    Protocol of the proxy connection (http or https)
* ProxyAddressFqdn

    Proxy fqdn to use. Empty means no proxy
* ProxyAddressPort

    Proxy port to use
* ProxyAddressExcludes

    Addresses that ignore proxy settings (e.g. local network) separated by a ','
    Wildcard * can be used.
    e.g: 127.0.0.1,localhost,10.*
* ExternalOcspServerFqdn

    Beame external Oscp server that also allows the communication of ntp. Required in case of proxy.
    e.g: iep9bs1p7cj3cmit.tl5h1ipgobrdqsj6.v1.p.beameio.net
    
* CustomGatekeeper

    Local path to the custom gatekeeper. The file needs to be a zip with the gatekeeper folder structure.
    When this property is not empty, after the installation of the released gatekeeper, this file will override the installed gatekeeper with the contained files.
* CustomGatekeeperCSS

    Local path to the custom gatekeeper css. The file needs to be a zip with the gatekeeper folder structure.
    Similar to the CustomGatekeeper setting, When this proterty is not empty, after the installation of the released gatekeeper and overriding by the customgatekeeper (if defined),  this file will override the installed gatekeeper with the contained files.

If any custom gatekeeper property is defined, the npm install and gulp options are executed on the final folder.

