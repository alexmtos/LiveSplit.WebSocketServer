using LiveSplit.Model;
using LiveSplit.WsServer.Protocol;
using LiveSplit.WsServer.State;
using System.Text.Json;

namespace LiveSplit.WsServer.Commands;

/// <summary>
///     Actions that edit the run (splits file) and its metadata.
/// </summary>
public static class RunCommands
{
    public static void Register(CommandDispatcher d)
    {
        d.Register("setsplitname", CommandAccess.Control,
            "Renames a split. Args: { index, name } (negative indices count from the end). Plain text: \"setsplitname <index> <name>\".",
            SetSplitName);
        d.Register("setcurrentsplitname", CommandAccess.Control, "Renames the current split. Args: { name }.", SetCurrentSplitName);
        d.Register("setcustomvariable", CommandAccess.Control,
            "Sets a custom variable of the run. Args: { name, value }. Plain text: 'setcustomvariable [\"name\", \"value\"]'.",
            SetCustomVariable);
        d.Register("setgamename", CommandAccess.Control, "Changes the game name. Args: { name }.", SetGameName);
        d.Register("setcategoryname", CommandAccess.Control, "Changes the category name. Args: { name }.", SetCategoryName);
        d.Register("setoffset", CommandAccess.Control, "Changes the start offset of the run. Only while the timer is not running. Args: { time }.", SetOffset);
    }

    private static object RenameSplit(CommandContext c, int index, string name)
    {
        int resolved = TimerCalculations.ResolveSplitIndex(c.State, index);
        if (resolved < 0 || resolved >= c.State.Run.Count)
        {
            throw CommandException.InvalidArgs($"Split index {index} is out of range (the run has {c.State.Run.Count} splits).");
        }

        c.State.Run[resolved].Name = name;
        c.State.Run.HasChanged = true;
        c.State.CallRunManuallyModified();
        return new { index = resolved, name };
    }

    private static object SetSplitName(CommandContext c, CommandArgs a)
    {
        if (a.IsText)
        {
            string[] parts = a.Text.Split([' '], 2);
            if (parts.Length < 2 || !int.TryParse(parts[0], out int textIndex))
            {
                throw CommandException.InvalidArgs("Usage: setsplitname <index> <name>");
            }

            return RenameSplit(c, textIndex, parts[1]);
        }

        int index = a.GetInt("index") ?? throw CommandException.InvalidArgs("Missing argument 'index'.");
        return RenameSplit(c, index, a.RequireString("name", 1));
    }

    private static object SetCurrentSplitName(CommandContext c, CommandArgs a)
    {
        if (c.State.CurrentSplit == null)
        {
            throw CommandException.InvalidPhase("There is no current split while the timer is not running.");
        }

        return RenameSplit(c, c.State.CurrentSplitIndex, a.RequireString("name"));
    }

    private static object SetCustomVariable(CommandContext c, CommandArgs a)
    {
        string name;
        string value;

        if (a.IsText)
        {
            // LiveSplit's built-in server uses a JSON array: ["name", "value"].
            try
            {
                string[] options = JsonSerializer.Deserialize<string[]>(a.Text);
                if (options == null || options.Length < 2)
                {
                    throw CommandException.InvalidArgs("Usage: setcustomvariable [\"name\", \"value\"]");
                }

                name = options[0];
                value = options[1];
            }
            catch (JsonException)
            {
                throw CommandException.InvalidArgs("Usage: setcustomvariable [\"name\", \"value\"]");
            }
        }
        else
        {
            name = a.RequireString("name");
            value = a.GetString("value", 1);
        }

        if (string.IsNullOrEmpty(name))
        {
            throw CommandException.InvalidArgs("Missing argument 'name'.");
        }

        c.State.Run.Metadata.SetCustomVariable(name, value);
        return new { name, value };
    }

    private static object SetGameName(CommandContext c, CommandArgs a)
    {
        string name = a.GetString("name") ?? "";
        c.State.Run.GameName = name;
        c.State.Run.HasChanged = true;
        c.State.CallRunManuallyModified();
        return new { gameName = name };
    }

    private static object SetCategoryName(CommandContext c, CommandArgs a)
    {
        string name = a.GetString("name") ?? "";
        c.State.Run.CategoryName = name;
        c.State.Run.HasChanged = true;
        c.State.CallRunManuallyModified();
        return new { categoryName = name };
    }

    private static object SetOffset(CommandContext c, CommandArgs a)
    {
        if (c.State.CurrentPhase != TimerPhase.NotRunning)
        {
            throw CommandException.InvalidPhase("The offset can only be changed while the timer is not running.");
        }

        c.State.Run.Offset = a.RequireTime("time");
        c.State.Run.HasChanged = true;
        c.State.CallRunManuallyModified();
        return new { startingOffset = TimeDto.Milliseconds(c.State.Run.Offset) };
    }
}
