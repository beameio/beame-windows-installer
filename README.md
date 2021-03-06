*2020-12 - The project is discontinued*

# beame-windows-installer  
[![Release](https://img.shields.io/github/v/release/beameio/beame-windows-installer.svg)](https://github.com/beameio/beame-windows-installer/releases) [![Build Status](https://travis-ci.org/beameio/beame-windows-installer.svg?branch=master)](https://travis-ci.org/beameio/beame-windows-installer) [![HitCount](http://hits.dwyl.io/beameio/beame-windows-installer.svg)](https://github.com/beameio/beame-windows-installer)

The Windows Installer tools are set of commands to install all prerequisites such as NPM, OpenSSL and more before installing the main component

This setup was designed to run on a windows 64 bits machine running .net framework 4.5 and was tested with windows server 2012 and 2016


## Running

The executable has to be run with Administrator permissions. If not given, it'll warn about the fact and exit.

### Interactive
In interactive mode, it'll display a menu with the available installation options
Currently they are:

    1. Gatekeeper
    2. BeameSDK
    4. Dependencies
    6. Uninstall
    9. Exit
 
 Typing the wanted number and pressing enter will allow the option to be executed.
 
 Option 4 installs just the dependencies, while option 1 and 2 installs the dependencies together with the described product. 
  
### Non-interactive
 Calling the executable with the option number as argument will allow an auto-select of that option. 


## Application Settings

Under the file (`app.config` in the source code, `BeameWindowsInstaller.exe.config` in the releases) installer settings can be configured.

To do so, add the properties as `<add key="GatekeeperName" value="Beame Gatekeeper"/>` inside the `<appSettings>` tag.

Please not that all values are text fields

### Installer settings
* InstallationFolder

    Installation folder to use for the nodejs, npm and gatekeeper installation. 
    
    Default is the current program files + beame (e.g "c:\Program Files\beame").
    
* EnableRegistrationTokenRequest

    Enables the request of the registration token during the installation for beame-gatekeeper or beame-sdk. Can be `true` or `false`. Default is `false`. Enabling this setting, automatically disables the EnableRegisterSiteOnFinish
 
* RegisterSiteOnFinish
    
    Site that is show in the end of the installation in order to allow registration.
    RegisterSiteOnFinish value needs to finish with no "/" since it'll be appended with "/gatekeeper" in case of gatekeeper installation.
    If empty or if EnableRegistrationTokenRequest is activated, it'll not be used

* InstallServiceAs
    
    User to install the service as. Can be `LocalSystem`, `LocalService`, `NetworkService` or `User`. 
    
    In case of `User`, the current user will be used and the password will be requested for the service installation. 
    
    Default is `User` 

### Proxy settings

* ProxyAddressProtocol

    Protocol of the proxy connection (`http` or `https`)
    
* ProxyAddressFqdn

    Proxy fqdn to use. Empty means no proxy
    
* ProxyAddressPort

    Proxy port to use
    
* ProxyAddressExcludes

    Addresses that ignore proxy settings (e.g. local network) separated by a ','
    Wildcard * can be used.
    e.g: `127.0.0.1,localhost,10.*`
    
* ExternalOcspServerFqdn

    Beame external Oscp server that also allows the communication of ntp. Required in case of proxy.
    e.g: `iep9bs1p7cj3cmit.tl5h1ipgobrdqsj6.v1.p.beameio.net`
    
### Gatekeeper settings
* VersionToInstall
    
    Version of the beame-gatekeeper or beame-sdk to install. Default is `latest`

* GatekeeperName

    Name of the gatekeeper, will identify the gatekeeper in the mobile app and ui. e.g `Beame Gatekeeper`

* GatekeeperMode

    Type of gatekeeper to install. Can be `Gatekeeper`  or  `CentralLogin`

* LogToFile
    
    Enables the gatekeeper to log to daily log files, located under .beame/logs. Can be `true` or `false`. Default is `true` 

* LogLevel

    Sets the log level. Can be `DEBUG`, `INFO`, `WARN`, `ERROR` or `FATAL`. Default is `INFO`

* EncryptUserData

    Configures gatekeeper option to encrypt user data. Can be `true` or `false`. Default is `true` 

* AllowDirectSignin

    Configures gatekeeper option to allow direct login from the mobile phone (no need for a browser). Can be `true` or `false`. Default is `true` 

* PublicRegistration

    Configures gatekeeper option to allow public user registration. Can be `true` or `false`. Default is `false`

* RegistrationImageRequired

    Configures gatekeeper option to require registration and login with user photo validation workload. Can be `true` or `false`. Default is `false` 

* AllowSignInWithCreds

    Configures gatekeeper option to show the sign in with client credentials. Can be `true` or `false`. Default is `true`

* AllowSignInWithUltrasound

    Configures gatekeeper option to show the sign in with ultrasound. Can be `true` or `false`. Default is `true` 

* DisableDemoServers

    Configures gatekeeper option to disable the demo servers. Can be `true` or `false`. Default is `false`

* AdvancedSettingsEnabled

    Configures gatekeeper option to show the advanced settings on the admin control panel. Can be `true` or `false`. Default is `false`

* ShowZendeskSupport

    Configures gatekeeper option to show the zendesk support. Can be `true` or `false`. Default is `false`

* CentralLoginUrl

    Configures the gatekeeper central login url to use. Empty (default) means to use the beame central login urls
    
* LogoutToCentralLogin
    
    Configures the gatekeeper to logout always to the central login url instead of the gatekeeper one (page where the user stays after a logout). Can be `true` or `false`. Default is `false`


### Gatekeeper customization 

* CustomGatekeeper

    Local path to the custom gatekeeper. The file needs to be a zip with the gatekeeper folder structure.
   
    When this property is not empty, this file will be used for the installation of the beame-gatekeeper.
    
* CustomGatekeeperCSS

    Local path to the custom gatekeeper css. The file needs to be a zip with the gatekeeper folder structure.
   
    Similar to the CustomGatekeeper setting, When this proterty is not empty, after the installation of the released gatekeeper and overriding by the customgatekeeper (if defined),  this file will override the installed gatekeeper with the contained files.

If any custom gatekeeper property is defined, the npm install and gulp options are executed on the final folder.


### Troubleshooting

* Windows service not starting with "Service didn't return an error":
    
    Make sure the user that the service is configured to use (InstallServiceAs) has enough permission to access the InstallationFolder.
    
* Installer fails on Visual Studio dependency:
   
   Make sure internet connection is available (if using proxy, that proxy is correctly configured in the options from `Proxy settings` section)
   
   Restart windows (for the case some pending installation reboot is required) and try installing again
