﻿using System.Diagnostics;
using System.Windows;
using PRM.Core;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol;
using Shawn.Utils;

namespace PRM.ViewModel
{
    public class VmServerListItem : NotifyPropertyChangedBase
    {
        private ProtocolServerBase _server = null;
        public ProtocolServerBase Server
        {
            get => _server;
            private set => SetAndNotifyIfChanged(nameof(Server), ref _server, value);
        }

        public VmServerListItem(ProtocolServerBase server)
        {
            Server = server;
        }


        private Visibility _visible = Visibility.Collapsed;
        public Visibility Visible
        {
            get => _visible;
            set => SetAndNotifyIfChanged(nameof(Visible), ref _visible, value);
        }



        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetAndNotifyIfChanged(nameof(IsSelected), ref _isSelected, value);
        }

        public bool IsDispNameEditing { get; set; } = false;

        private RelayCommand _cmdIsEditingToggle;
        public RelayCommand CmdIsEditingToggle
        {
            get
            {
                if (_cmdIsEditingToggle == null)
                {
                    _cmdIsEditingToggle = new RelayCommand((o) =>
                    {
                        string param = o?.ToString();
                        if (string.IsNullOrEmpty(param))
                        {
                            IsDispNameEditing = false;
                            RaisePropertyChanged(nameof(IsDispNameEditing));
                        }
                        else
                            switch (param)
                            {
                                case nameof(Server.DispName):
                                    IsDispNameEditing = !IsDispNameEditing;
                                    RaisePropertyChanged(nameof(IsDispNameEditing));
                                    break;
                            }
                    }, o => this.Server.Id > 0);
                }
                return _cmdIsEditingToggle;
            }
        }





        #region CMD
        private RelayCommand _cmdConnServer;
        public RelayCommand CmdConnServer
        {
            get
            {
                if (_cmdConnServer == null)
                    _cmdConnServer = new RelayCommand((o) =>
                    {
                        GlobalEventHelper.OnRequireServerConnect?.Invoke(Server.Id);
                    });
                return _cmdConnServer;
            }
        }

        private RelayCommand _cmdEditServer;
        public RelayCommand CmdEditServer
        {
            get
            {
                return _cmdEditServer ??= new RelayCommand((o) =>
                {
                    GlobalEventHelper.OnGoToServerEditPage?.Invoke(Server.Id, false, true);
                });
            }
        }


        private RelayCommand _cmdDuplicateServer;
        public RelayCommand CmdDuplicateServer
        {
            get
            {
                return _cmdDuplicateServer ??= new RelayCommand((o) =>
                {
                    GlobalEventHelper.OnGoToServerEditPage?.Invoke(Server.Id, true, true);
                });
            }
        }

        private RelayCommand _cmdDeleteServer;

        public RelayCommand CmdDeleteServer
        {
            get
            {
                return _cmdDeleteServer ??= new RelayCommand((o) =>
                {
                    if (MessageBoxResult.Yes == MessageBox.Show(
                            SystemConfig.Instance.Language.GetText("string_delete_confirm"),
                            SystemConfig.Instance.Language.GetText("string_delete_confirm_title"),
                            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.None)
                        )
                    {
                        GlobalData.Instance.ServerListRemove(Server);
                    }
                });
            }
        }
        #endregion
    }
}
