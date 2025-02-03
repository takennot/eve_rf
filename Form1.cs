using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace EVE_RF
{
    public partial class RF : Form
    {
        string stationsData = Path.Combine(Application.StartupPath, "Data", "stations_expanded.csv");
        string selectedStation = "";
        private static readonly HttpClient httpClient = new HttpClient();

        public RF()
        {
            InitializeComponent();
            LoadStations(stationsData);
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private async Task DownloadData(string fileUrl, string destinationFolder, string filename)
        {
            try
            {
                if (!Directory.Exists(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                HttpResponseMessage response = await httpClient.GetAsync(fileUrl);
                response.EnsureSuccessStatusCode();

                byte[] fileBytes = await response.Content.ReadAsByteArrayAsync();

                string filepath = Path.Combine(destinationFolder, filename);

                File.WriteAllBytes(filepath, fileBytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                throw;
            }
        }

        private async Task LoadStations(string filepath)
        {
            List<string> stations = new List<string>();

            string fileUrl = "https://takennot.github.io/assets/stations_expanded.csv";
            string destinationFolder = Path.Combine(Application.StartupPath, "Data");
            string filename = "stations_expanded.csv";

            if (File.Exists(filepath))
            {
                try
                {
                    //read csv & skip head lines
                    var lines = File.ReadAllLines(filepath).Skip(1);
                    foreach (var line in lines) 
                    {
                        var columns = line.Split(';');
                        if (columns.Length >= 8) 
                        { 
                            stations.Add(columns[7].Trim()); 
                        }
                    }

                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error reading DB file: " + ex.Message);
                }
            }
            else
            {
                DialogResult downloadAnswer = MessageBox.Show(
                                                "Stations Data not found. Download now?",
                                                "No stations data",
                                                MessageBoxButtons.YesNo);
                switch (downloadAnswer)
                {
                    case DialogResult.Yes:
                        await DownloadData(fileUrl, destinationFolder, filename);
                        Application.Restart();
                        // add line below if it doesn't restart
                        // Environment.Exit(0);
                            break;
                    case DialogResult.No:
                        break;
                    default:
                        break;
                }
            }

            SetAutoCompleteSource(tbxStationName, stations);
        }

        private void SetAutoCompleteSource(TextBox textBox, List<string> items)
        {
            var autoCompleteCollection = new AutoCompleteStringCollection();
            autoCompleteCollection.AddRange(items.ToArray());

            textBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            textBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
            textBox.AutoCompleteCustomSource = autoCompleteCollection;
        }

        private void btnFindStation_Click(object sender, EventArgs e)
        {
            GetStationData();
        }

        private T GetStationLocalData<T>(int columnIndex, Func<string, T> converter)
        {
            selectedStation = tbxStationName.Text.Trim();

            if (string.IsNullOrEmpty(selectedStation))
            {
                MessageBox.Show("Please enter a station name");
                return default(T);
            }

            try
            {
                if (File.Exists(stationsData))
                {
                    var lines = File.ReadAllLines(stationsData).Skip(1); // Skip the header row
                    var result = lines.Select(line => line.Split(';'))
                                      .FirstOrDefault(columns => columns.Length >= 8 &&
                                        columns[7].Trim().Equals(selectedStation, StringComparison.OrdinalIgnoreCase));

                    if (result != null)
                    {
                        string value = result[columnIndex];
                        try
                        {
                            return converter(value);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Error converting value: " + ex.Message);
                            return default(T);
                        }
                    }
                    else
                    {
                        MessageBox.Show($"Station '{selectedStation}' not found.");
                        return default(T);
                    }
                }
                else
                {
                    MessageBox.Show("DB file not found.");
                    return default(T);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading DB file: " + ex.Message);
                return default(T);
            }
        }

        // re-write in the future
        private async void GetStationData()
        {
            int stationID = GetStationLocalData<int>(0, value => Int32.Parse(value));
            string apiUrl = $"https://esi.evetech.net/latest/universe/stations/{stationID}/?datasource=tranquility";

            int stationType = GetStationLocalData<int>(2, value => Int32.Parse(value));
            string imageUrl = $"https://images.evetech.net/types/{stationType}/render?size=128";

            int regionID = GetStationLocalData<int>(6, value => Int32.Parse(value));
            string regionUrl = $"https://esi.evetech.net/latest/universe/regions/{regionID}/?datasource=tranquility";

            double systemSecurity = GetStationLocalData<double>(1, value =>
            {
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double securityValue))
                {
                    return Math.Round(securityValue, 1, MidpointRounding.AwayFromZero);
                }
                else
                {
                    return -5;
                }
            });

            try
            {
                var stationPreview = await GetDataAsync<Image>(imageUrl, isImage: true);
                if (stationPreview != null) 
                {
                    pbxStationPreview.SizeMode = PictureBoxSizeMode.AutoSize;
                    pbxStationPreview.Image = stationPreview;
                }
                else
                {
                    pbxStationPreview.Image = null;
                }

                var stationData = await GetDataAsync<Station>(apiUrl);
                if (stationData != null)
                {
                    stationName.Text = stationData.Name;
                    //Parago Station check (no offices)
                    if (stationType == 71361)
                    {
                        officeRent.Text = "Paragon Station (No Offices)";
                    }
                    else
                    {
                        officeRent.Text = stationData.OfficeRentalCost.ToString("N0") + " ISK";
                    }
                    stationServices.Text = FormatServices(stationData.StationServices);

                    // this section can likely be replaced with another local database
                    int systemID = stationData.SystemID;
                    string apiUrlSystem = $"https://esi.evetech.net/latest/universe/systems/{systemID}/?datasource=tranquility";

                    var systemData = await GetDataAsync<SolarSystem>(apiUrlSystem);
                    if (systemData != null)
                    {
                        stationSystem.Text = systemData.Name + "(" + systemSecurity + ")";
                    }
                    else
                    {
                        MessageBox.Show("No data returned");
                    }

                    lnkDotlan.Links.Clear();
                    lnkDotlan.Links.Add(0, lnkDotlan.Text.Length, $"https://evemaps.dotlan.net/system/{FormatSystemName(systemData.Name)}");
                    lnkZkill.Links.Clear();
                    lnkZkill.Links.Add(0, lnkZkill.Text.Length, $"https://zkillboard.com/system/{systemID}/");

                    var regionData = await GetDataAsync<Region>(regionUrl);
                    stationRegion.Text = regionData.Name;
                }
                else
                {
                    MessageBox.Show("No data returned");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private async Task<T?> GetDataAsync<T>(string url, bool isImage = false)
        {
            HttpResponseMessage response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            if (isImage)
            {
                // If image
                byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
                using (var ms = new MemoryStream(imageBytes))
                {
                    return (T)(object)Image.FromStream(ms);  // Cast image to T
                }
            }
            else
            {
                // if normal
                string jsonResponse = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(jsonResponse);
            }
        }

        private string FormatServices(List<string> services)
        {
            string servicesFormatted = "";
            foreach (string service in services) 
            { 
                servicesFormatted += service + " | ";
            }

            return servicesFormatted;
        }

        private string FormatSystemName(string name)
        {
            return name.Replace(" ", "_");
        }

        private void lnkDotlan_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Open the link using the default web browser
            string target = e.Link.LinkData as string;

            if (!string.IsNullOrEmpty(target))
            {
                try
                {
                    System.Diagnostics.Process.Start(new ProcessStartInfo
                    {
                        FileName = target,
                        UseShellExecute = true // for modern systems
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open link: {ex.Message}");
                }
            }
        }

        private void lnkZkill_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Open the link using the default web browser
            string target = e.Link.LinkData as string;

            if (!string.IsNullOrEmpty(target))
            {
                try
                {
                    System.Diagnostics.Process.Start(new ProcessStartInfo
                    {
                        FileName = target,
                        UseShellExecute = true // Important for modern systems
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open link: {ex.Message}");
                }
            }
        }
    }
}