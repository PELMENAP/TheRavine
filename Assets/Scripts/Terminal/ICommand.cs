using System;
using Cysharp.Text;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;

using TheRavine.Generator;
using TheRavine.EntityControl;

namespace TheRavine.Base
{
    public interface ICommand
    {
        string Name { get; }
        string ShortName { get; }
        string Description { get; }
        UniTask ExecuteAsync(string[] args, CommandContext context);
    }

    public interface IValidatedCommand : ICommand
    {
        bool Validate(CommandContext context);
    }
}