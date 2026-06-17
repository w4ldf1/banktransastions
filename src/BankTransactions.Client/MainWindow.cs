using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using BankTransactions.Shared;

namespace BankTransactions.Client;

public sealed class MainWindow : Window
{
    private readonly ApiClient _api;
    private readonly TextBlock _status = new() { Text = "Not connected" };
    private readonly ComboBox _mode = Choice("ORM", "SQL");

    private readonly ListBox _customerList = new();
    private readonly TextBox _customerSearch = Input("Search");
    private readonly ComboBox _customerType = Choice("Individual", "Legal");
    private readonly TextBox _customerName = Input("Full name");
    private readonly TextBox _customerTaxId = Input("Tax id");
    private readonly TextBox _customerEmail = Input("Email");
    private readonly TextBox _customerPhone = Input("Phone");

    private readonly ListBox _partnerList = new();
    private readonly TextBox _partnerSearch = Input("Search");
    private readonly TextBox _partnerName = Input("Name");
    private readonly TextBox _partnerCountry = Input("Country");
    private readonly TextBox _partnerSwift = Input("SWIFT");
    private readonly TextBox _partnerFee = Input("Fee %");
    private readonly CheckBox _partnerForeign = new() { Content = "Foreign partner" };

    private readonly ListBox _accountList = new();
    private readonly TextBox _accountSearch = Input("Search");
    private readonly TextBox _accountCustomerId = Input("Customer id");
    private readonly ComboBox _accountType = Choice("Salary", "Currency", "Savings");
    private readonly TextBox _accountCurrency = Input("Currency");
    private readonly TextBox _accountOpeningBalance = Input("Opening balance");
    private readonly TextBox _accountPartnerId = Input("Partner bank id");
    private readonly CheckBox _accountActive = new() { Content = "Active", IsChecked = true };

    private readonly ListBox _transactionList = new();
    private readonly TextBox _transactionSearch = Input("Search");
    private readonly TextBox _transactionFromId = Input("From account id");
    private readonly TextBox _transactionToId = Input("To account id");
    private readonly TextBox _transactionPartnerId = Input("Partner bank id");
    private readonly TextBox _transactionAmount = Input("Amount");
    private readonly TextBox _transactionCurrency = Input("Currency");
    private readonly TextBox _transactionDescription = Input("Description");

    public MainWindow()
    {
        Title = "Bank Transactions - Variant 9";
        Width = 1180;
        Height = 760;
        MinWidth = 960;
        MinHeight = 620;

        var apiUrl = Environment.GetEnvironmentVariable("BANK_API_URL") ?? "http://localhost:5055";
        _api = new ApiClient(new Uri(apiUrl));
        _mode.SelectedIndex = 0;
        ShowLogin();
    }

    private void ShowLogin()
    {
        var username = Input("Username");
        username.Text = "admin";
        var password = Input("Password");
        password.PasswordChar = '*';
        password.Text = "admin123";
        var loginStatus = new TextBlock { Text = "Not connected" };

        var panel = new StackPanel
        {
            Width = 360,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var loginButton = PrimaryButton("Login");
        loginButton.Click += async (_, _) =>
        {
            try
            {
                var auth = await _api.LoginAsync(Text(username), Text(password));
                BuildWorkspace();
                await RefreshAllAsync();
                SetStatus($"Connected as {auth.Username} ({auth.Role}) to {_api.BaseAddress}");
            }
            catch (Exception ex)
            {
                if (ReferenceEquals(Content, panel))
                {
                    loginStatus.Text = ex.Message;
                    return;
                }

                SetStatus(ex.Message);
            }
        };

        panel.Children.Add(Header("Bank Transactions"));
        panel.Children.Add(Muted("Variant 9"));
        panel.Children.Add(Field("Username", username));
        panel.Children.Add(Field("Password", password));
        panel.Children.Add(loginButton);
        panel.Children.Add(loginStatus);
        Content = panel;
    }

    private void BuildWorkspace()
    {
        var refreshButton = SecondaryButton("Refresh");
        refreshButton.Click += async (_, _) => await RunAsync(RefreshAllAsync);

        var topBar = new DockPanel
        {
            LastChildFill = false,
            Margin = new Avalonia.Thickness(12),
        };
        var title = Header("Bank Transactions");
        DockPanel.SetDock(title, Dock.Left);
        topBar.Children.Add(title);

        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        controls.Children.Add(new TextBlock
        {
            Text = "DB access",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.DimGray
        });
        controls.Children.Add(_mode);
        controls.Children.Add(refreshButton);
        DockPanel.SetDock(controls, Dock.Right);
        topBar.Children.Add(controls);

        var tabs = new TabControl
        {
            ItemsSource = new[]
            {
                new TabItem { Header = "Customers", Content = BuildCustomersTab() },
                new TabItem { Header = "Accounts", Content = BuildAccountsTab() },
                new TabItem { Header = "Transactions", Content = BuildTransactionsTab() },
                new TabItem { Header = "Partner banks", Content = BuildPartnersTab() }
            }
        };

        var shell = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(topBar, Dock.Top);
        DockPanel.SetDock(_status, Dock.Bottom);
        _status.Margin = new Avalonia.Thickness(12, 6);
        shell.Children.Add(topBar);
        shell.Children.Add(_status);
        shell.Children.Add(tabs);
        Content = shell;
    }

    private Control BuildCustomersTab()
    {
        _customerList.SelectionChanged += (_, _) =>
        {
            if (Selected<CustomerDto>(_customerList) is not { } item)
            {
                return;
            }

            _customerType.SelectedItem = item.CustomerType;
            _customerName.Text = item.FullName;
            _customerTaxId.Text = item.TaxId;
            _customerEmail.Text = item.Email;
            _customerPhone.Text = item.Phone;
        };

        var add = PrimaryButton("Add");
        add.Click += async (_, _) => await RunAsync(async () =>
        {
            await _api.PostAsync<CustomerCreateRequest, CustomerDto>(
                "/api/customers",
                Mode,
                new CustomerCreateRequest(SelectedText(_customerType), Text(_customerName), Text(_customerTaxId), Text(_customerEmail), Text(_customerPhone)));
            await RefreshCustomersAsync();
        });

        var update = SecondaryButton("Update");
        update.Click += async (_, _) => await RunAsync(async () =>
        {
            var item = RequiredSelected<CustomerDto>(_customerList);
            await _api.PutAsync<CustomerUpdateRequest, CustomerDto>(
                "/api/customers",
                item.Id,
                Mode,
                new CustomerUpdateRequest(SelectedText(_customerType), Text(_customerName), Text(_customerTaxId), Text(_customerEmail), Text(_customerPhone)));
            await RefreshCustomersAsync();
        });

        var delete = DangerButton("Delete");
        delete.Click += async (_, _) => await RunAsync(async () =>
        {
            var item = RequiredSelected<CustomerDto>(_customerList);
            await _api.DeleteAsync("/api/customers", item.Id, Mode);
            await RefreshCustomersAsync();
        });

        return TwoPane(
            ListPanel(_customerSearch, _customerList, RefreshCustomersAsync),
            FormPanel("Customer", Field("Type", _customerType), Field("Full name", _customerName), Field("Tax id", _customerTaxId),
                Field("Email", _customerEmail), Field("Phone", _customerPhone), Buttons(add, update, delete)));
    }

    private Control BuildPartnersTab()
    {
        _partnerList.SelectionChanged += (_, _) =>
        {
            if (Selected<PartnerBankDto>(_partnerList) is not { } item)
            {
                return;
            }

            _partnerName.Text = item.Name;
            _partnerCountry.Text = item.Country;
            _partnerSwift.Text = item.SwiftCode;
            _partnerFee.Text = item.FeePercent.ToString("0.####");
            _partnerForeign.IsChecked = item.IsForeign;
        };

        var add = PrimaryButton("Add");
        add.Click += async (_, _) => await RunAsync(async () =>
        {
            await _api.PostAsync<PartnerBankCreateRequest, PartnerBankDto>(
                "/api/partner-banks",
                Mode,
                new PartnerBankCreateRequest(Text(_partnerName), Text(_partnerCountry), Text(_partnerSwift), Bool(_partnerForeign), Decimal(_partnerFee)));
            await RefreshPartnersAsync();
        });

        var update = SecondaryButton("Update");
        update.Click += async (_, _) => await RunAsync(async () =>
        {
            var item = RequiredSelected<PartnerBankDto>(_partnerList);
            await _api.PutAsync<PartnerBankUpdateRequest, PartnerBankDto>(
                "/api/partner-banks",
                item.Id,
                Mode,
                new PartnerBankUpdateRequest(Text(_partnerName), Text(_partnerCountry), Text(_partnerSwift), Bool(_partnerForeign), Decimal(_partnerFee)));
            await RefreshPartnersAsync();
        });

        var delete = DangerButton("Delete");
        delete.Click += async (_, _) => await RunAsync(async () =>
        {
            var item = RequiredSelected<PartnerBankDto>(_partnerList);
            await _api.DeleteAsync("/api/partner-banks", item.Id, Mode);
            await RefreshPartnersAsync();
        });

        return TwoPane(
            ListPanel(_partnerSearch, _partnerList, RefreshPartnersAsync),
            FormPanel("Partner bank", Field("Name", _partnerName), Field("Country", _partnerCountry), Field("SWIFT", _partnerSwift),
                Field("Fee %", _partnerFee), _partnerForeign, Buttons(add, update, delete)));
    }

    private Control BuildAccountsTab()
    {
        _accountCurrency.Text = "RUB";
        _accountOpeningBalance.Text = "0";
        _accountList.SelectionChanged += (_, _) =>
        {
            if (Selected<AccountDto>(_accountList) is not { } item)
            {
                return;
            }

            _accountCustomerId.Text = item.CustomerId.ToString();
            _accountType.SelectedItem = item.AccountType;
            _accountCurrency.Text = item.Currency;
            _accountPartnerId.Text = item.PartnerBankId?.ToString() ?? string.Empty;
            _accountActive.IsChecked = item.IsActive;
            _accountOpeningBalance.Text = item.Balance.ToString("0.##");
        };

        var add = PrimaryButton("Add");
        add.Click += async (_, _) => await RunAsync(async () =>
        {
            await _api.PostAsync<AccountCreateRequest, AccountDto>(
                "/api/accounts",
                Mode,
                new AccountCreateRequest(Int(_accountCustomerId), SelectedText(_accountType), Text(_accountCurrency), Decimal(_accountOpeningBalance), NullableInt(_accountPartnerId)));
            await RefreshAccountsAsync();
        });

        var update = SecondaryButton("Update");
        update.Click += async (_, _) => await RunAsync(async () =>
        {
            var item = RequiredSelected<AccountDto>(_accountList);
            await _api.PutAsync<AccountUpdateRequest, AccountDto>(
                "/api/accounts",
                item.Id,
                Mode,
                new AccountUpdateRequest(SelectedText(_accountType), Text(_accountCurrency), Bool(_accountActive), NullableInt(_accountPartnerId)));
            await RefreshAccountsAsync();
        });

        var delete = DangerButton("Delete");
        delete.Click += async (_, _) => await RunAsync(async () =>
        {
            var item = RequiredSelected<AccountDto>(_accountList);
            await _api.DeleteAsync("/api/accounts", item.Id, Mode);
            await RefreshAccountsAsync();
        });

        return TwoPane(
            ListPanel(_accountSearch, _accountList, RefreshAccountsAsync),
            FormPanel("Account", Field("Customer id", _accountCustomerId), Field("Type", _accountType), Field("Currency", _accountCurrency),
                Field("Opening balance", _accountOpeningBalance), Field("Partner bank id", _accountPartnerId), _accountActive, Buttons(add, update, delete)));
    }

    private Control BuildTransactionsTab()
    {
        _transactionCurrency.Text = "RUB";
        _transactionList.SelectionChanged += (_, _) =>
        {
            if (Selected<TransactionDto>(_transactionList) is not { } item)
            {
                return;
            }

            _transactionFromId.Text = item.FromAccountId?.ToString() ?? string.Empty;
            _transactionToId.Text = item.ToAccountId?.ToString() ?? string.Empty;
            _transactionPartnerId.Text = item.PartnerBankId?.ToString() ?? string.Empty;
            _transactionAmount.Text = item.Amount.ToString("0.##");
            _transactionCurrency.Text = item.Currency;
            _transactionDescription.Text = item.Description;
        };

        var add = PrimaryButton("Create");
        add.Click += async (_, _) => await RunAsync(async () =>
        {
            await _api.PostAsync<TransactionCreateRequest, TransactionDto>(
                "/api/transactions",
                Mode,
                new TransactionCreateRequest(NullableInt(_transactionFromId), NullableInt(_transactionToId), NullableInt(_transactionPartnerId),
                    Decimal(_transactionAmount), Text(_transactionCurrency), Text(_transactionDescription)));
            await RefreshTransactionsAsync();
            await RefreshAccountsAsync();
        });

        var update = SecondaryButton("Update description");
        update.Click += async (_, _) => await RunAsync(async () =>
        {
            var item = RequiredSelected<TransactionDto>(_transactionList);
            await _api.PutAsync<TransactionUpdateRequest, TransactionDto>(
                "/api/transactions",
                item.Id,
                Mode,
                new TransactionUpdateRequest(Text(_transactionDescription)));
            await RefreshTransactionsAsync();
        });

        var delete = DangerButton("Delete");
        delete.Click += async (_, _) => await RunAsync(async () =>
        {
            var item = RequiredSelected<TransactionDto>(_transactionList);
            await _api.DeleteAsync("/api/transactions", item.Id, Mode);
            await RefreshTransactionsAsync();
            await RefreshAccountsAsync();
        });

        return TwoPane(
            ListPanel(_transactionSearch, _transactionList, RefreshTransactionsAsync),
            FormPanel("Transaction", Field("From account id", _transactionFromId), Field("To account id", _transactionToId),
                Field("Partner bank id", _transactionPartnerId), Field("Amount", _transactionAmount), Field("Currency", _transactionCurrency),
                Field("Description", _transactionDescription), Buttons(add, update, delete)));
    }

    private async Task RefreshAllAsync()
    {
        await RefreshCustomersAsync();
        await RefreshPartnersAsync();
        await RefreshAccountsAsync();
        await RefreshTransactionsAsync();
        SetStatus($"Data refreshed via {Mode.ToUpperInvariant()}");
    }

    private async Task RefreshCustomersAsync()
    {
        var rows = await _api.SearchAsync<CustomerDto>("/api/customers", Mode, _customerSearch.Text);
        _customerList.ItemsSource = rows.Select(item => new DisplayItem<CustomerDto>(
            item,
            $"{item.Id} | {item.CustomerType} | {item.FullName} | tax {item.TaxId}")).ToList();
        SetStatus($"Customers: {rows.Count}");
    }

    private async Task RefreshPartnersAsync()
    {
        var rows = await _api.SearchAsync<PartnerBankDto>("/api/partner-banks", Mode, _partnerSearch.Text);
        _partnerList.ItemsSource = rows.Select(item => new DisplayItem<PartnerBankDto>(
            item,
            $"{item.Id} | {item.Name} | {item.Country} | {item.SwiftCode} | fee {item.FeePercent:0.####}%")).ToList();
        SetStatus($"Partner banks: {rows.Count}");
    }

    private async Task RefreshAccountsAsync()
    {
        var rows = await _api.SearchAsync<AccountDto>("/api/accounts", Mode, _accountSearch.Text);
        _accountList.ItemsSource = rows.Select(item => new DisplayItem<AccountDto>(
            item,
            $"{item.Id} | {item.AccountNumber} | {item.CustomerName} | {item.AccountType} | {item.Currency} {item.Balance:0.##}")).ToList();
        SetStatus($"Accounts: {rows.Count}");
    }

    private async Task RefreshTransactionsAsync()
    {
        var rows = await _api.SearchAsync<TransactionDto>("/api/transactions", Mode, _transactionSearch.Text);
        _transactionList.ItemsSource = rows.Select(item => new DisplayItem<TransactionDto>(
            item,
            $"{item.Id} | {item.Status} | {item.Currency} {item.Amount:0.##} | fee {item.FeeAmount:0.##} | {item.Description}")).ToList();
        SetStatus($"Transactions: {rows.Count}");
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
    }

    private string Mode => SelectedText(_mode).Equals("SQL", StringComparison.OrdinalIgnoreCase) ? "sql" : "orm";

    private void SetStatus(string value)
    {
        _status.Text = value;
    }

    private static Control TwoPane(Control list, Control form)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("3*,2*"),
            RowDefinitions = new RowDefinitions("*"),
            Margin = new Avalonia.Thickness(12)
        };
        grid.Children.Add(list);
        Grid.SetColumn(form, 1);
        grid.Children.Add(form);
        return grid;
    }

    private static Control ListPanel(TextBox search, ListBox list, Func<Task> refresh)
    {
        var refreshButton = SecondaryButton("Search");
        refreshButton.Click += async (_, _) =>
        {
            try
            {
                await refresh();
            }
            catch
            {
                // The caller status bar handles normal UI actions; this path is only a fallback for ad-hoc refreshes.
            }
        };

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 0, 12, 10)
        };
        toolbar.Children.Add(search);
        toolbar.Children.Add(refreshButton);

        var panel = new DockPanel { LastChildFill = true, Margin = new Avalonia.Thickness(0, 0, 12, 0) };
        DockPanel.SetDock(toolbar, Dock.Top);
        panel.Children.Add(toolbar);
        panel.Children.Add(list);
        return panel;
    }

    private static Control FormPanel(string title, params Control[] controls)
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(SubHeader(title));
        foreach (var control in controls)
        {
            stack.Children.Add(control);
        }

        return new ScrollViewer
        {
            Content = stack,
            Padding = new Avalonia.Thickness(12, 0, 0, 0)
        };
    }

    private static Control Buttons(params Button[] buttons)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        foreach (var button in buttons)
        {
            panel.Children.Add(button);
        }

        return panel;
    }

    private static Control Field(string label, Control input)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock { Text = label, Foreground = Brushes.DimGray, FontSize = 12 });
        stack.Children.Add(input);
        return stack;
    }

    private static TextBox Input(string watermark)
    {
        return new TextBox
        {
            Watermark = watermark,
            MinWidth = 180,
            Height = 34
        };
    }

    private static ComboBox Choice(params string[] values)
    {
        return new ComboBox
        {
            ItemsSource = values,
            SelectedIndex = 0,
            MinWidth = 140,
            Height = 34
        };
    }

    private static Button PrimaryButton(string text)
    {
        return new Button
        {
            Content = text,
            MinWidth = 92,
            Height = 34,
            Background = new SolidColorBrush(Color.Parse("#1F6FEB")),
            Foreground = Brushes.White
        };
    }

    private static Button SecondaryButton(string text)
    {
        return new Button
        {
            Content = text,
            MinWidth = 86,
            Height = 34
        };
    }

    private static Button DangerButton(string text)
    {
        return new Button
        {
            Content = text,
            MinWidth = 86,
            Height = 34,
            Foreground = new SolidColorBrush(Color.Parse("#B42318"))
        };
    }

    private static TextBlock Header(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 22,
            FontWeight = FontWeight.SemiBold
        };
    }

    private static TextBlock SubHeader(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Margin = new Avalonia.Thickness(0, 0, 0, 4)
        };
    }

    private static TextBlock Muted(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = Brushes.DimGray
        };
    }

    private static T? Selected<T>(ListBox list)
    {
        return list.SelectedItem is DisplayItem<T> item ? item.Value : default;
    }

    private static T RequiredSelected<T>(ListBox list)
    {
        return Selected<T>(list) ?? throw new InvalidOperationException("Select a row first.");
    }

    private static string SelectedText(ComboBox box)
    {
        return box.SelectedItem?.ToString() ?? string.Empty;
    }

    private static string Text(TextBox input)
    {
        return input.Text?.Trim() ?? string.Empty;
    }

    private static bool Bool(CheckBox checkBox)
    {
        return checkBox.IsChecked == true;
    }

    private static int Int(TextBox input)
    {
        if (!int.TryParse(Text(input), out var value))
        {
            throw new InvalidOperationException("Integer value is required.");
        }

        return value;
    }

    private static int? NullableInt(TextBox input)
    {
        var text = Text(input);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!int.TryParse(text, out var value))
        {
            throw new InvalidOperationException("Integer value is required.");
        }

        return value;
    }

    private static decimal Decimal(TextBox input)
    {
        var text = Text(input).Replace(',', '.');
        if (!decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException("Decimal value is required.");
        }

        return value;
    }

    private sealed record DisplayItem<T>(T Value, string Text)
    {
        public override string ToString()
        {
            return Text;
        }
    }
}
