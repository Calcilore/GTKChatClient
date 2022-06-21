using System.Collections.Generic;
using System.Linq;

namespace ChatAppClient;

public static class Commands {
    private static readonly Dictionary<string, string> emoticons = new Dictionary<string, string> {
        {"shrug", "¯\\_(ツ)_/¯"},
        {"lenny", "( ͡° ͜ʖ ͡°)"},
        {"flip", "(╯°□°)╯︵"},
        {"tableflip", "(╯°□°)╯︵"},
        {"table", "(╯°□°)╯︵ "},
        {"unflip", "┳━┳ ヽ(ಠل͜ಠ)ﾉ"},
        {"uwu", "ヾ(●ω●)ノ"},
        {"happy", "😂"},
        {"sad", "😢"},
        {"angry", "😠"},
        {"sick", "😷"},
        {"dance", "💃"},
        {"wave", "( ͡❛ ͜ʖ ͡❛)✊"},
        {"breasts", "(.)(.)"},
        {"fish", "ӽe̲̅v̲̅o̲̅l̲̅u̲̅t̲̅i̲̅o̲̅ɳ̲̅ᕗ"},
        {"$1", "[̲̅$̲̅(̲̅1̲̅)̲̅$̲̅"},
        {"$5", "[̲̅$̲̅(̲̅5̲̅)̲̅$̲̅"},
        {"$10", " [̲̅$̲̅(̲̅1̲̅0̲̅)̲̅$̲̅"},
        {"$100", "[̲̅$̲̅(̲̅ιοο̲̅)̲̅$̲̅"},
        {"disapproval", "ಠ_ಠ"},
        {"bat", "◥▅◤"},
        {"kiss", "(๑ˇεˇ๑)"},
        {"flowergirl", "(◕‿◕✿)"},
        {"crying", "( ༎ຶ ۝ ༎ຶ )"},
        {"cat", "(=ʘᆽʘ=)∫"},
        {"bear", "ʕ •ᴥ•ʔ"},
        {"shootingstar", "☆彡"},
        {"kick", "＼| ￣ヘ￣|／＿＿＿＿＿＿＿θ☆( *o*)/"}
    };

    public static string Emoticons(string text) {
        // replace all emoticons in text
        return emoticons
            .Aggregate(text, (current, emoticon) => 
                current.Replace("#" + emoticon.Key, emoticon.Value));
    }
}