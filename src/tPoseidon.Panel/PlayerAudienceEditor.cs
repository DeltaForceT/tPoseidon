using Avalonia.Controls;
using tPoseidon.Hosting;
using tPoseidon.Hosting.Servers;

namespace tPoseidon.Panel;

public sealed class PlayerAudienceEditor : StackPanel
{
    private readonly HostSettings _settings;
    private readonly ChoiceCard[] _cards;
    private readonly TextBox _address = WizardPage.Box("");
    private readonly TextBlock _addressLabel = new() { FontSize = Tokens.FontSizeCaption, Foreground = Tokens.TextSecondary };
    private readonly StackPanel _addressBox = new() { Spacing = 4 };
    private readonly Banner _ports = new();
    private readonly Banner _error = new();
    private PlayerAudience _audience;

    public PlayerAudienceEditor(HostSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Spacing = 14;
        _cards =
        [
            new ChoiceCard("Only this computer", "You play alone, or test. The servers listen on 127.0.0.1."),
            new ChoiceCard("Friends on my network", "Other computers in your home or office connect over the local network."),
            new ChoiceCard("Anyone on the internet", "Players connect with your public IP or a domain name. Needs port forwarding."),
        ];
        for (var i = 0; i < _cards.Length; i++)
        {
            var audience = (PlayerAudience)i;
            _cards[i].Chosen += () => Select(audience);
            Children.Add(_cards[i]);
        }

        if (settings.Network.Audience != PlayerAudience.Local)
        {
            _address.Text = settings.Network.PublicAddress;
        }

        _addressBox.Children.Add(_addressLabel);
        _addressBox.Children.Add(_address);
        _address.TextChanged += (_, _) =>
        {
            WizardPage.Mark(_address, false);
            _error.Hide();
        };
        Children.Add(_addressBox);
        Children.Add(_error);
        Children.Add(_ports);
        Select(settings.Network.Audience);
    }

    public PlayerAudience Audience => _audience;

    public TextBox AddressBox => _address;

    public void Select(PlayerAudience audience)
    {
        var previous = _audience;
        _audience = audience;
        for (var i = 0; i < _cards.Length; i++)
        {
            _cards[i].Selected = i == (int)audience;
        }

        _addressBox.IsVisible = audience != PlayerAudience.Local;
        _addressLabel.Text = audience == PlayerAudience.Lan ? "Address of this computer on your network" : "Public address players connect to (your public IP or domain)";
        var lanDefault = NetworkAddresses.DefaultLan(_settings);
        if (audience == PlayerAudience.Lan && string.IsNullOrWhiteSpace(_address.Text))
        {
            _address.Text = lanDefault;
        }

        if (audience == PlayerAudience.Internet && previous == PlayerAudience.Lan && _address.Text == lanDefault)
        {
            _address.Text = "";
        }

        _error.Hide();
        if (audience == PlayerAudience.Local)
        {
            _ports.Hide();
        }
        else
        {
            var ports = ServerCatalog.PlayerFacingPorts(_settings);
            _ports.Show(
                BannerKind.Info,
                $"Open these TCP ports in your firewall{(audience == PlayerAudience.Internet ? " and forward them on your router" : "")}: {string.Join(", ", ports)}",
                $"On {OsHint.Name}: {OsHint.Firewall(ports)}");
        }
    }

    public bool TryApply()
    {
        var address = _address.Text?.Trim() ?? "";
        if (_audience != PlayerAudience.Local)
        {
            if (address.Length == 0)
            {
                WizardPage.Mark(_address, true);
                _error.Show(BannerKind.Error, "No address given.", _audience == PlayerAudience.Lan
                    ? "Enter the address of this computer on your network."
                    : "Enter your public IP address or a domain name that points to this computer.");
                return false;
            }

            if (Uri.CheckHostName(address) == UriHostNameType.Unknown)
            {
                WizardPage.Mark(_address, true);
                _error.Show(BannerKind.Error, $"'{address}' is not an IP address or a domain name.", "Use a form like 203.0.113.7 or play.example.org, without http:// or a port.");
                return false;
            }
        }

        _settings.Network.Audience = _audience;
        _settings.Network.PublicAddress = _audience == PlayerAudience.Local ? NetworkAddresses.Loopback : address;
        return true;
    }
}
