using System;
using Cysharp.Text;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

using TheRavine.Generator;
using TheRavine.EntityControl;

namespace TheRavine.Base
{
    public class CommandContext : IDisposable
    {
        private const int MaxLines = 50;
        private readonly Queue<string> _lines = new Queue<string>(MaxLines);
        private Utf16ValueStringBuilder _sb = ZString.CreateStringBuilder();
        private bool _disposed = false;

        public TextMeshProUGUI OutputWindow { get; }
        public PlayerEntity PlayerData { get; private set; }
        public MapGenerator Generator { get; private set; }
        public GameObject Graphy { get; private set; }

        public CommandContext(TextMeshProUGUI output, PlayerEntity player, MapGenerator gen, GameObject graphy)
        {
            OutputWindow = output;
            PlayerData = player;
            Generator = gen;
            Graphy = graphy;

            if (!string.IsNullOrEmpty(output.text))
            {
                var current = output.text.Split('\n');
                foreach (var line in current)
                    _lines.Enqueue(line);
                _sb.Append(output.text);
            }
        }

        public void Display(string message)
        {
            if (_disposed) return;
            
            _lines.Enqueue(message);
            if (_lines.Count > MaxLines)
                _lines.Dequeue();

            _sb.Clear();
            foreach (var line in _lines)
                _sb.AppendLine(line);

            OutputWindow.text = _sb.ToString();
        }

        public void Clear()
        {
            if (_disposed) return;
            
            _lines.Clear();
            _sb = ZString.CreateStringBuilder();
            OutputWindow.text = string.Empty;
        }
        
        public void SetPlayer(PlayerEntity player) => PlayerData = player;
        public void SetGenerator(MapGenerator gen) => Generator = gen;
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _sb.Dispose();
                _disposed = true;
            }
        }
    }
}