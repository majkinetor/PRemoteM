﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.View;
using Shawn.Utils;
using Shawn.Utils.PageHost;
using NotifyPropertyChangedBase = PRM.Core.NotifyPropertyChangedBase;

namespace PRM.ViewModel
{
    public class VmMain : NotifyPropertyChangedBase
    {
        private string _dispNameFilter = "";
        public string DispNameFilter
        {
            get => _dispNameFilter;
            set
            {
                SetAndNotifyIfChanged(nameof(DispNameFilter), ref _dispNameFilter, value);
                if (PageServerList?.VmDataContext?.DispNameFilter != null)
                    PageServerList.VmDataContext.DispNameFilter = value;
            }
        }
        private AnimationPage _bottomPage = null;
        public AnimationPage BottomPage
        {
            get => _bottomPage;
            set => SetAndNotifyIfChanged(nameof(BottomPage), ref _bottomPage, value);
        }


        private AnimationPage _dispPage = null;
        public AnimationPage DispPage
        {
            get => _dispPage;
            set => SetAndNotifyIfChanged(nameof(DispPage), ref _dispPage, value);
        }
        private AnimationPage _topPage = null;

        public AnimationPage TopPage
        {
            get => _topPage;
            set => SetAndNotifyIfChanged(nameof(TopPage), ref _topPage, value);
        }


        private ServerListPage _pageServerList;
        public ServerListPage PageServerList
        {
            get => _pageServerList;
            set => SetAndNotifyIfChanged(nameof(PageServerList), ref _pageServerList, value);
        }



        private bool _sysOptionsMenuIsOpen = false;
        public bool SysOptionsMenuIsOpen
        {
            get => _sysOptionsMenuIsOpen;
            set => SetAndNotifyIfChanged(nameof(SysOptionsMenuIsOpen), ref _sysOptionsMenuIsOpen, value);
        }

        private int _progressBarValue = 0;
        public int ProgressBarValue
        {
            get => _progressBarValue;
            set => SetAndNotifyIfChanged(nameof(ProgressBarValue), ref _progressBarValue, value);
        }

        private int _progressBarMaximum = 0;
        public int ProgressBarMaximum
        {
            get => _progressBarMaximum;
            set
            {
                if (value != _progressBarMaximum)
                {
                    SetAndNotifyIfChanged(nameof(ProgressBarMaximum), ref _progressBarMaximum, value);
                }
            }
        }

        private string _progressBarInfo = "";
        public string ProgressBarInfo
        {
            get => _progressBarInfo;
            set
            {
                if (value != _progressBarInfo)
                {
                    SetAndNotifyIfChanged(nameof(ProgressBarInfo), ref _progressBarInfo, value);
                }
            }
        }

        public readonly MainWindow Window;


        public VmMain(MainWindow window)
        {
            Window = window;
            PageServerList = new ServerListPage(this);
            BottomPage = new AnimationPage()
            {
                InAnimationType = AnimationPage.InOutAnimationType.SlideFromRight,
                OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
#if DEBUG
                Page = new ServerManagementPage(this),
#endif
            };

            GlobalEventHelper.OnLongTimeProgress += (arg1, arg2, arg3) =>
            {
                ProgressBarValue = arg1;
                ProgressBarMaximum = arg2;
                ProgressBarInfo = arg2 > 0 ? arg3 : "";
            };
            GlobalEventHelper.OnGoToServerEditPage += (id, isDuplicate, isInAnimationShow) =>
            {
                ProtocolServerBase server;
                if (id <= 0)
                {
                    server = new ProtocolServerNone();
                }
                else
                {
                    Debug.Assert(GlobalData.Instance.VmItemList.Any(x => x.Server.Id == id));
                    server = GlobalData.Instance.VmItemList.First(x => x.Server.Id == id).Server;
                }
                DispPage = new AnimationPage()
                {
                    InAnimationType = isInAnimationShow ? AnimationPage.InOutAnimationType.SlideFromRight : AnimationPage.InOutAnimationType.None,
                    OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                    Page = new ServerEditorPage(new VmServerEditorPage(server, PageServerList.VmDataContext, isDuplicate)),
                };

                Window.ActivateMe();
            };
        }



        #region CMD

        private RelayCommand _cmdGoSysOptionsPage;
        public RelayCommand CmdGoSysOptionsPage
        {
            get
            {
                if (_cmdGoSysOptionsPage == null)
                {
                    _cmdGoSysOptionsPage = new RelayCommand((o) =>
                    {
                        DispPage = new AnimationPage()
                        {
                            InAnimationType = AnimationPage.InOutAnimationType.SlideFromRight,
                            OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                            Page = new SystemConfigPage(this, (Type)o),
                        };
                        SysOptionsMenuIsOpen = false;
                    }, o => DispPage?.Page?.GetType() != typeof(SystemConfigPage));
                }
                return _cmdGoSysOptionsPage;
            }
        }


        private RelayCommand _cmdGoAboutPage;
        public RelayCommand CmdGoAboutPage
        {
            get
            {
                if (_cmdGoAboutPage == null)
                {
                    _cmdGoAboutPage = new RelayCommand((o) =>
                    {
                        DispPage = new AnimationPage()
                        {
                            InAnimationType = AnimationPage.InOutAnimationType.SlideFromRight,
                            OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                            Page = new AboutPage(this),
                        };
                        SysOptionsMenuIsOpen = false;

                    }, o => DispPage?.Page?.GetType() != typeof(AboutPage));
                }
                return _cmdGoAboutPage;
            }
        }




        private RelayCommand _cmdGoServerListPage;
        public RelayCommand CmdGoServerListPage
        {
            get
            {
                if (_cmdGoServerListPage == null)
                {
                    _cmdGoServerListPage = new RelayCommand((o) =>
                    {
                        if (DispPage?.Page?.GetType() != null)
                            DispPage = null;
                    }, o => DispPage?.Page?.GetType() != null && DispPage?.Page?.GetType() != typeof(ServerListPage));
                }
                return _cmdGoServerListPage;
            }
        }
        #endregion
    }
}
