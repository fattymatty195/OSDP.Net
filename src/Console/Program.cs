﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Console.Configuration;
using log4net;
using log4net.Config;
using OSDP.Net;
using OSDP.Net.Connections;
using Terminal.Gui;

namespace Console
{
    internal static class Program
    {
        private static readonly ControlPanel ControlPanel = new ControlPanel();
        private static readonly Queue<string> Messages = new Queue<string>();
        private static readonly object MessageLock = new object();

        private static readonly MenuBarItem DevicesMenuBarItem =
            new MenuBarItem("_Devices", new[]
            {
                new MenuItem("_Add", string.Empty, AddDevice),
                // new MenuItem("_List", "", AddDevice),
                // new MenuItem("_Send Command", "", AddDevice),
                new MenuItem("_Remove", string.Empty, RemoveDevice)
            });

        private static Guid _connectionId;
        private static Window _window;
        private static MenuBar _menuBar;

        private static Settings _settings;

        private static void Main()
        {
            XmlConfigurator.Configure(
                LogManager.GetRepository(Assembly.GetAssembly(typeof(LogManager))),
                new FileInfo("log4net.config"));

            _settings = GetConnectionSettings();

            Application.Init();

            _window = new Window("OSDP.Net")
            {
                X = 0,
                Y = 1, // Leave one row for the toplevel menu

                Width = Dim.Fill(),
                Height = Dim.Fill() - 1
            };

            _menuBar = new MenuBar(new[]
            {
                new MenuBarItem("_System", new[]
                {
                    new MenuItem("_Start Connection", "", StartConnection),
                    new MenuItem("Sto_p Connection", "", ControlPanel.Shutdown),
                    new MenuItem("Show _Log", string.Empty, ShowLog),
                    new MenuItem("Save _Configuration", "", () => SetConnectionSettings(_settings)),
                    new MenuItem("_Quit", "", () =>
                    {
                        if (MessageBox.Query(40, 10, "Quit", "Quit program?", "Yes", "No") == 0)
                        {
                            Application.RequestStop();
                        }
                    })
                }),
                DevicesMenuBarItem,
                new MenuBarItem("_Commands", new[]
                {
                    new MenuItem("_Device Capabilities", "", 
                        () => SendCommand("Device Capabilities Command", _connectionId, ControlPanel.DeviceCapabilities)),
                    new MenuItem("_ID Report", "", 
                        () => SendCommand("ID Report Command", _connectionId, ControlPanel.IdReport)),
                    new MenuItem("_Local Status", "", 
                        () => SendCommand("Local Status Command", _connectionId, ControlPanel.LocalStatus)),
                })
            });

            Application.Top.Add(_menuBar, _window);

            Application.Run();

            ControlPanel.Shutdown();
        }

        private static void ShowLog()
        {
            _window.RemoveAll();
            var scrollView = new ScrollView(new Rect(1, 0, _window.Frame.Width - 1, _window.Frame.Height - 1))
            {
                ContentSize = new Size(100, 100),
                ShowVerticalScrollIndicator = true,
                ShowHorizontalScrollIndicator = true
            };
            lock (MessageLock)
            {
                scrollView.Add(new Label(0, 0, string.Join("", Messages.Reverse().ToArray())));
            }

            _window.Add(scrollView);
        }

        private static void StartConnection()
        {
            var portNameTextField = new TextField(15, 1, 35, _settings.ConnectionSettings.PortName);
            var baudRateTextField = new TextField(15, 3, 35, _settings.ConnectionSettings.BaudRate.ToString());

            void StartConnectionButtonClicked()
            {
                _settings.ConnectionSettings.PortName = portNameTextField.Text.ToString();
                if (!int.TryParse(baudRateTextField.Text.ToString(), out var baudRate))
                {

                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid baud rate entered!", "OK");
                    return;
                }

                _settings.ConnectionSettings.BaudRate = baudRate;

                ControlPanel.Shutdown();
                _connectionId = ControlPanel.StartConnection(
                    new SerialPortOsdpConnection(_settings.ConnectionSettings.PortName,
                        _settings.ConnectionSettings.BaudRate));
                foreach (var device in _settings.Devices)
                {
                    ControlPanel.AddDevice(_connectionId, device.Address, device.UseSecureChannel);
                }
                Application.RequestStop();
            }

            Application.Run(new Dialog("Start Connection", 60, 10,
                new Button("Start") {Clicked = StartConnectionButtonClicked},
                new Button("Cancel") {Clicked = Application.RequestStop})
            {
                new Label(1, 1, "Port:"),
                portNameTextField,
                new Label(1, 3, "Baud Rate:"),
                baudRateTextField
            });
        }

        public static void AddLogMessage(string message)
        {
            lock (MessageLock)
            {
                Messages.Enqueue(message);
                while (Messages.Count > 100)
                {
                    Messages.Dequeue();
                }
            }
        }

        private static Settings GetConnectionSettings()
        {
            try
            {
                string json = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.config"));
                return JsonSerializer.Deserialize<Settings>(json);
            }
            catch
            {
                return new Settings();
            }
        }

        private static void SetConnectionSettings(Settings connectionSettings)
        {
            try
            {
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.config"),
                    JsonSerializer.Serialize(connectionSettings));
            }
            catch
            {
                // ignored
            }
        }

        private static void AddDevice()
        {
            var nameTextField = new TextField(15, 1, 35, string.Empty);
            var addressTextField = new TextField(15, 3, 35, string.Empty);
            var useSecureChannelCheckBox = new CheckBox(1, 5, "Use Secure Channel", true);

            void AddDeviceButtonClicked()
            {
                if (!byte.TryParse(addressTextField.Text.ToString(), out var address))
                {

                    MessageBox.ErrorQuery(40, 10, "Error", "Invalid address entered!", "OK");
                    return;
                }

                if (_settings.Devices.Any(device => device.Address == address))
                {
                    if (MessageBox.Query(60, 10, "Overwrite", "Device already exists at that address, overwrite?", "Yes", "No") == 1)
                    {
                        return;
                    }
                }
                ControlPanel.AddDevice(_connectionId, address, useSecureChannelCheckBox.Checked);
                _settings.Devices.Add(new DeviceSetting
                {
                    Address = address, Name = nameTextField.Text.ToString(),
                    UseSecureChannel = useSecureChannelCheckBox.Checked
                });
                Application.RequestStop();
            }

            Application.Run(new Dialog("Add Device", 60, 13,
                new Button("Add") {Clicked = AddDeviceButtonClicked},
                new Button("Cancel") {Clicked = Application.RequestStop})
            {
                new Label(1, 1, "Name:"),
                nameTextField,
                new Label(1, 3, "Address:"),
                addressTextField,
                useSecureChannelCheckBox
            });
        }

        private static void RemoveDevice()
        {
            var orderedDevices = _settings.Devices.OrderBy(device => device.Address).ToArray();
            var scrollView = new ScrollView(new Rect(6, 1, 40, 6))
            {
                ContentSize = new Size(50, orderedDevices.Length * 2),
                ShowVerticalScrollIndicator = orderedDevices.Length > 6,
                ShowHorizontalScrollIndicator = false
            };

            var deviceRadioGroup = new RadioGroup(0, 0,
                orderedDevices.Select(device => $"{device.Address} : {device.Name}").ToArray());
            scrollView.Add(deviceRadioGroup);
            
            void RemoveDeviceButtonClicked()
            {
                var removedDevice = orderedDevices[deviceRadioGroup.Selected];
                ControlPanel.RemoveDevice(_connectionId, removedDevice.Address);
                _settings.Devices.Remove(removedDevice);
                Application.RequestStop();
            }

            Application.Run(new Dialog("Remove Device", 60, 13,
                new Button("Remove") {Clicked = RemoveDeviceButtonClicked},
                new Button("Cancel") {Clicked = Application.RequestStop})
            {
                scrollView
            });
        }

        private static void SendCommand<T>(string title, Guid connectionId, Func<Guid, byte, Task<T>> sendCommandFunction)
        {
            var orderedDevices = _settings.Devices.OrderBy(device => device.Address).ToArray();
            var scrollView = new ScrollView(new Rect(6, 1, 40, 6))
            {
                ContentSize = new Size(50, orderedDevices.Length * 2),
                ShowVerticalScrollIndicator = orderedDevices.Length > 6,
                ShowHorizontalScrollIndicator = false
            };

            var deviceRadioGroup = new RadioGroup(0, 0,
                orderedDevices.Select(device => $"{device.Address} : {device.Name}").ToArray());
            scrollView.Add(deviceRadioGroup);

            async Task SendCommandButtonClicked()
            {
                var selectedDevice = orderedDevices[deviceRadioGroup.Selected];
                try
                {
                    string resultString = (await sendCommandFunction(connectionId, selectedDevice.Address)).ToString();
                    var resultStringLines = resultString.Split(Environment.NewLine);

                    var resultsView = new ScrollView(new Rect(5, 1, 50, 6))
                    {
                        ContentSize = new Size(resultStringLines.OrderByDescending(line => line.Length).First().Length,
                            resultStringLines.Length),
                        ShowVerticalScrollIndicator = true,
                        ShowHorizontalScrollIndicator = true
                    };
                    resultsView.Add(new Label(resultString));

                    Application.Run(new Dialog(title, 60, 13,
                        new Button("OK") {Clicked = Application.RequestStop})
                    {
                        resultsView
                    });
                }
                catch (Exception exception)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", exception.Message, "OK");
                }

                Application.RequestStop();
            }

            Application.Run(new Dialog(title, 60, 13,
                new Button("Send") {Clicked = async () =>
                    {
                        await SendCommandButtonClicked();
                    }
                },
                new Button("Cancel") {Clicked = Application.RequestStop})
            {
                scrollView
            });
        }
    }
}

