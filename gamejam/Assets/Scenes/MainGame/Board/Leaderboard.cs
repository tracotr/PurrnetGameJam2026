using System.Collections.Generic;
using System.Text;
using PurrNet;
using TMPro;
using UnityEngine;

public class Leaderboard : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private int maxRows = 8;
    [SerializeField] private int maxNameLength = 8;
    
    private readonly List<(PlayerID player, int dunked)> _standings = new();
    private readonly StringBuilder _sb = new();
    private CoinManager _coins;
    private string _last;

    private void OnDisable()
    {
        _coins = null;
        _last = null;
    }

    private void TryBind()
    {
        if (_coins != null) return;
        InstanceHandler.TryGetInstance(out _coins);
    }

    private void LateUpdate()
    {
        TryBind();
        if (!_coins) return;
        Rebuild();
    }

    private void Rebuild()
    {
        _coins.GetStandings(_standings);

        _sb.Clear();

        int count = Mathf.Min(_standings.Count, maxRows);
        for (int i = 0; i < count; i++)
        {
            var entry = _standings[i];
            _sb.Append(i + 1).Append('.')
                .Append("<pos=10%>").Append(Truncate(PlayerNameRegistry.Get(entry.player)))
                .Append("<pos=55%>").Append(entry.dunked);
            
            if (i < count - 1)
                _sb.Append('\n');
        }

        string result = _sb.ToString();
        if (result == _last) return;

        _last = result;
        text.text = result;
    }
    
    private string Truncate(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length <= maxNameLength)
            return name;

        return name.Substring(0, maxNameLength - 1) + "…";
    }
}