using System;
using System.Collections.Generic;
using Windows.Networking.Connectivity;

namespace QingtingRadio.Tools
{
    class NetworkManager
    {
        public string NetworkTitle
        {
            get
            {
                if (_network == 0)
                {
                    return "2G";
                }
                else if (_network == 1)
                {
                    return "3G";
                }
                else if (_network == 2)
                {
                    return "4G";
                }
                else if (_network == 3)
                {
                    return "WIFI";
                }
                else if (_network == 4)
                {
                    return "未知";
                }
                else
                {
                    return "无网络访问";
                }
            }
        }

        private int _network;

        public int Network
        {
            get
            {
                return _network; 
            }
        }

        public static bool IsNetConnected()
        {
            if(NetworkManager.Current._network == 5)
            {
                return false;
            }

            return true;
        }

        public event NetworkStatusChangedEventHandler NetworkStatusChanged;
        private NetworkManager()
        {
            NetworkInformation.NetworkStatusChanged += NetworkInformation_NetworkStatusChanged;
            _network = GetConnectionGeneration();
        }

        private void NetworkInformation_NetworkStatusChanged(object sender)
        {
            _network = GetConnectionGeneration();
            NetworkStatusChanged?.Invoke(this);
        }

        /// <summary>
        ///  0:2G 1:3G 2:4G  3:wifi  4:未知 5:无连接
        /// </summary>
        private int GetConnectionGeneration()
        {
            ConnectionProfile profile = NetworkInformation.GetInternetConnectionProfile();
            if(profile == null)
            {
                return 5;
            }
            else
            {
                NetworkConnectivityLevel cLevel = profile.GetNetworkConnectivityLevel();
                if(cLevel == NetworkConnectivityLevel.None)
                {
                    return 5;
                }
            }
            if (profile.IsWwanConnectionProfile)
            {
                if (profile.WwanConnectionProfileDetails == null)
                {
                    return 4;
                }

                WwanDataClass connectionClass = profile.WwanConnectionProfileDetails.GetCurrentDataClass();
                switch (connectionClass)
                {
                    //2G-equivalent
                    case WwanDataClass.Edge:
                    case WwanDataClass.Gprs:
                        return 0;
                    //3G-equivalent
                    case WwanDataClass.Cdma1xEvdo:
                    case WwanDataClass.Cdma1xEvdoRevA:
                    case WwanDataClass.Cdma1xEvdoRevB:
                    case WwanDataClass.Cdma1xEvdv:
                    case WwanDataClass.Cdma1xRtt:
                    case WwanDataClass.Cdma3xRtt:
                    case WwanDataClass.CdmaUmb:
                    case WwanDataClass.Umts:
                    case WwanDataClass.Hsdpa:
                    case WwanDataClass.Hsupa:
                        return 1;
                    //4G-equivalent
                    case WwanDataClass.LteAdvanced:
                        return 2;

                    //not connected
                    case WwanDataClass.None:
                        return 4;

                    //unknown
                    case WwanDataClass.Custom:
                    default:
                        return 4;
                }
            }
            else if (profile.IsWlanConnectionProfile)
            {
                return 3;
            }
            else
            {
                return 4;
            }

        }

        public static readonly NetworkManager Current = new NetworkManager();
    }
}
