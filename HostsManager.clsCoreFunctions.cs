﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using HostsManager.Settings;
using System.Security.Principal;
using System.Net;
using System.Security.AccessControl;
using System.Windows.Forms;
using System.Diagnostics;
using System.ServiceProcess;
using System.Net.NetworkInformation;
using System.Management;

namespace HostsManager
{
    public static class clsCoreFunctions
    {


        //The update process
        public static void updateHostsFile()
        {
            bool err = false;
            Thread start = null;
            try
            {
                if (clsSettingsData.urls.Count == 0 && clsSettingsData.UseCustomBlacklist && !clsSettingsData.isHidden)
                {
                    frmDialog d = new frmDialog();
                    d.action = "Your personal hosts file is empty.";
                    d.showButton = true;
                    d.ShowDialog();
                    err = true;
                }
                else
                {
                    //Get name of admin group
                    SecurityIdentifier id = new SecurityIdentifier("S-1-5-32-544");
                    string adminGroupName = id.Translate(typeof(NTAccount)).Value;

                    
                    if (!clsSettingsData.isHidden && !err)
                    {
                        start = new Thread(new ParameterizedThreadStart(clsUtilitys.Dialogs.showDialog));
                        start.Start("Downloading hosts file(s)...");
                    }

                    String fileText = "";
                    //Read hosts files from web
                    if (clsSettingsData.UseCustomBlacklist)
                    {
                        foreach (String u in clsSettingsData.urls)
                            fileText += "\r\n" + clsUtilitys.Downloader.DownloadString(u);
                    }
                    if (clsSettingsData.UseStevensBlacklist)
                    {
                        fileText += clsUtilitys.Downloader.DownloadString(
                         "https://raw.githubusercontent.com/StevenBlack/hosts/master/hosts");
                        if (clsSettingsData.showFakeNews)
                            fileText += clsUtilitys.Downloader.DownloadString(
                             "https://raw.githubusercontent.com/StevenBlack/hosts/master/extensions/fakenews/hosts");
                        if (clsSettingsData.showGambling)
                            fileText += clsUtilitys.Downloader.DownloadString(
                             "https://raw.githubusercontent.com/StevenBlack/hosts/master/extensions/gambling/hosts");
                        if (clsSettingsData.showPorn)
                        {
                            fileText += clsUtilitys.Downloader.DownloadString(
                             "https://raw.githubusercontent.com/StevenBlack/hosts/master/extensions/porn/clefspeare13/hosts");
                            fileText += clsUtilitys.Downloader.DownloadString(
                             "https://raw.githubusercontent.com/StevenBlack/hosts/master/extensions/porn/sinfonietta/hosts");
                        }
                        if (clsSettingsData.showSocial)
                            fileText += clsUtilitys.Downloader.DownloadString(
                             "https://raw.githubusercontent.com/StevenBlack/hosts/master/extensions/social/hosts");
                    }
                    if (clsSettingsData.UseHostsFileBlacklist)
                    {
                        fileText += clsUtilitys.Downloader.DownloadString("https://hosts-file.net/download/hosts.txt");
                        fileText += clsUtilitys.Downloader.DownloadString("https://hosts-file.net/hphosts-partial.txt");
                    }
                    if (!clsSettingsData.isHidden)
                        clsUtilitys.Dialogs.closeDialog();
                    if (!clsSettingsData.isHidden)
                    {
                        start = new Thread(new ParameterizedThreadStart(clsUtilitys.Dialogs.showDialog));
                        start.Start("Replacing IP's");
                    }

                    //IP Overwrite
                    if (clsSettingsData.replaceMethod == clsSettingsData.mIPReplaceMethod.KEEP_LOCALHOST)
                        clsSettingsData.ipTo = clsSettingsData.ipFrom;
                    else if (clsSettingsData.replaceMethod == clsSettingsData.mIPReplaceMethod.SET_CUSTOM)
                    {
                        clsSettingsData.ipTo = clsSettingsData.replaceIP;
                    }
                    else
                    {
                        clsSettingsData.ipTo = clsBrandingData.DefaultBlankHost;
                        clsSettingsData.ipTo = Dns.GetHostAddresses(clsSettingsData.ipTo)[0].ToString();
                    }
                    if (clsSettingsData.replaceMethod != clsSettingsData.mIPReplaceMethod.KEEP_LOCALHOST)
                    {
                        fileText = fileText.Replace("0.0.0.0", clsSettingsData.ipTo);
                        fileText = fileText.Replace("127.0.0.1", clsSettingsData.ipTo);
                        fileText = fileText.Replace("::1", clsSettingsData.ipTo);
                        fileText = fileText.Replace("0\t", clsSettingsData.ipTo + "\t");
                    }
                    foreach (String host in clsSettingsData.addHosts)
                        fileText += "\r\n" + clsSettingsData.ipTo + " " + host;

                    //CR/LF detection
                    fileText = fileText.Replace("\r", "");
                    fileText = fileText.Replace("\n", "\r\n");
                    if (!clsSettingsData.isHidden)
                        clsUtilitys.Dialogs.closeDialog();
                    //Write temp hosts file
                    System.IO.File.Delete("hosts.tmp");
                    System.IO.File.WriteAllText("hosts.tmp", fileText);
                    //Set permissions of hosts file to be writable by group admins
                    FileSecurity fs = clsUtilitys.HostsFile.setHostsFilePermissions();
                    //Copy hosts file to real hosts file
                    System.IO.File.Copy("hosts.tmp",
                     Environment.GetEnvironmentVariable("windir") + "\\system32\\drivers\\etc\\hosts", true);
                    System.IO.File.Delete("hosts.tmp");
                    //Reset permissions
                    clsUtilitys.HostsFile.resetHostsFilePermissions(fs);

                }
                if (!clsSettingsData.isHidden && !err)
                {
                    frmDialog f = new frmDialog();
                    f.showButton = true;
                    f.action = "Hosts file updated.";
                    f.ShowDialog();
                }
            }
            catch (Exception ex) //Hosts file update
            {
                if (start != null)
                    start.Abort();
                //generate error string
                String add = "";
                if (!clsSettingsData.isHidden && !err)
                    if (clsUtilitys.Info.isAntivir())
                    {
                        frmNotifyAntivirus f = new frmNotifyAntivirus();
                        f.ShowDialog();
                    }
                if (!clsSettingsData.isHidden && !err)
                    MessageBox.Show("Error: " + add + ex.Message);
            }
        }

        //Create Task for auto update
        public static void doAutoUpdate()
        {
            //prepare ProcessStartInfo
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            psi.FileName = "schtasks.exe";
            if (clsSettingsData.autoUpdate)
            {
                //Create task using schtasks.exe
                //FileSecurity fs=setHostsFilePermissions(); !!! 080417DH - muss evtl wieder reingemacht werden.
                String Arguments = "/Create /tn \"LV-Crew HostsManager\" /tr \"" +
                 System.Reflection.Assembly.GetEntryAssembly().Location +
                 " /auto\" /sc DAILY /RL HIGHEST /F";
                Process p = clsUtilitys.executeNoWindow("schtasks.exe", Arguments);
            }
            else
            {
                //Delete task using schtasks.exe                
                clsUtilitys.executeNoWindow("schtasks.exe", "/Delete /tn LV-Crew.HostsManager /F");
            }
        }

        public static void disableDNSService(frmCore frm)
        {
            System.Threading.Thread start = null;
            bool err = false;
            start = new Thread(new ParameterizedThreadStart(clsUtilitys.Dialogs.showDialog));
            start.Start("Disabling DNS-Client Service...");
            ServiceController _ServiceController = new ServiceController("dnscache");
            if (!_ServiceController.ServiceHandle.IsInvalid)
            {
                try
                {
                    _ServiceController.Stop();
                    _ServiceController.WaitForStatus(ServiceControllerStatus.Stopped,
                     TimeSpan.FromMilliseconds(10000));
                }
                catch (Exception)
                {
                    err = true;
                }
                try
                {
                    if (!err)
                    {
                        Process p = clsUtilitys.executeNoWindow("sc.exe", "config dnscache start=disabled");
                        p.WaitForExit();
                        clsSettingsData.DNServiceDisabled = true;
                        frm.setServiceButtonText("Enable DNS Service");
                    }
                }
                catch (Exception)
                {
                    err = true;
                }
            }
            if (start != null && start.ThreadState == System.Threading.ThreadState.Running)
                start.Abort();
            if (!err)
                clsUtilitys.Dialogs.showOKDIalog("DNS-Client service disabled.");
            else
                clsUtilitys.Dialogs.showOKDIalog("Erro disabling DNS-Client service. Please retry..");
        }


        public static void setDNS(String DNS)
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if ((nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet) || (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)) //&& (nic.OperationalStatus == OperationalStatus.Up))
                {

                    ManagementClass objMC = new ManagementClass("Win32_NetworkAdapterConfiguration");
                    ManagementObjectCollection objMOC = objMC.GetInstances();
                    foreach (ManagementObject objMO in objMOC)
                    {
                        if ((bool)objMO["IPEnabled"])
                        {
                            try
                            {
                                ManagementBaseObject newDNS =
                                 objMO.GetMethodParameters("SetDNSServerSearchOrder");
                                newDNS["DNSServerSearchOrder"] = DNS.Split(',');
                                ManagementBaseObject setDNS =
                                 objMO.InvokeMethod("SetDNSServerSearchOrder", newDNS, null);
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                        }
                    }
                }
            }
        }
    }
}
