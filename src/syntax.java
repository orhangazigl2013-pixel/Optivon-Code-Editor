package src;

import java.awt.Color;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import javax.swing.JTextPane;
import javax.swing.text.*;

public class syntax {

    private static final Color KEYWORD_COLOR = new Color(86, 156, 214);
    private static final Color FG_DEFAULT = new Color(220, 220, 220);

    public static void highlight(JTextPane editor) {
        StyledDocument doc = editor.getStyledDocument();
        String text;
        try {
            text = doc.getText(0, doc.getLength());
        } catch (BadLocationException e) {
            return;
        }

        // Varsayılan metin rengini sıfırla
        Style defaultStyle = editor.addStyle("DefaultStyle", null);
        StyleConstants.setForeground(defaultStyle, FG_DEFAULT);
        doc.setCharacterAttributes(0, text.length(), defaultStyle, true);

        // Keyword Renk Stili
        Style keywordStyle = editor.addStyle("KeywordStyle", null);
        StyleConstants.setForeground(keywordStyle, KEYWORD_COLOR);

        // C++ / Genel Anahtar Kelimeler
        String regex = "\\b(int|float|double|char|void|bool|string|auto|return|class|struct|enum|union|interface|namespace|"
                + "public|private|protected|virtual|override|static|const|if|else|for|while|do|switch|case|break|continue|"
                + "using|include|import|export|from|default|extends|def|function|fn|func|var|let|async|await|try|catch|finally|"
                + "throw|raise|new|delete|true|false|null|nullptr|None|undefined|print|std)\\b";

        Pattern pattern = Pattern.compile(regex);
        Matcher matcher = pattern.matcher(text);

        while (matcher.find()) {
            doc.setCharacterAttributes(matcher.start(), matcher.end() - matcher.start(), keywordStyle, false);
        }
    }
}