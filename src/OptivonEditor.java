package src;

import javax.swing.*;
import javax.swing.event.DocumentEvent;
import javax.swing.event.DocumentListener;
import java.awt.*;
import java.awt.datatransfer.DataFlavor;
import java.awt.dnd.*;
import java.awt.event.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.List;

public class OptivonEditor extends JFrame {

    private JTextPane editPane;
    private File currentFile = null;
    private static final Color BG_DARK = new Color(30, 30, 30);
    private static final Color FG_LIGHT = new Color(220, 220, 220);

    public OptivonEditor(String initialFilePath) {
        setTitle("Optivon Code Editor");
        setSize(900, 650);
        setDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);
        setLocationRelativeTo(null);

        // Editör Alanı Yapılandırması
        editPane = new JTextPane();
        editPane.setBackground(BG_DARK);
        editPane.setForeground(FG_LIGHT);
        editPane.setCaretColor(Color.WHITE);
        editPane.setFont(new Font("Consolas", Font.PLAIN, 18));

        JScrollPane scrollPane = new JScrollPane(editPane);
        scrollPane.getViewport().setBackground(BG_DARK);
        add(scrollPane, BorderLayout.CENTER);

        // Menü Çubuğu
        setupMenuBar();

        // Kısayollar (F5, Ctrl+S, Ctrl+N vb.)
        setupShortcuts();

        // Sözdizimi Renklendirme Dinleyicisi
        editPane.getDocument().addDocumentListener(new DocumentListener() {
            public void insertUpdate(DocumentEvent e) { runHighlight(); }
            public void removeUpdate(DocumentEvent e) { runHighlight(); }
            public void changedUpdate(DocumentEvent e) {}
        });

        // Sürükle - Bırak (Drag and Drop) Desteği
        setupDragAndDrop();

        // Birlikte Aç (Argüman ile gelen dosyayı açma)
        if (initialFilePath != null && !initialFilePath.isEmpty()) {
            loadFile(new File(initialFilePath));
        }
    }

    private void runHighlight() {
        SwingUtilities.invokeLater(() -> syntax.highlight(editPane));
    }

    private void setupMenuBar() {
        JMenuBar menuBar = new JMenuBar();

        JMenu fileMenu = new JMenu("Dosya");
        JMenuItem itemNew = new JMenuItem("Yeni (Ctrl+N)");
        JMenuItem itemOpen = new JMenuItem("Aç... (Ctrl+O)");
        JMenuItem itemSave = new JMenuItem("Kaydet (Ctrl+S)");
        JMenuItem itemSaveAs = new JMenuItem("Farklı Kaydet...");
        JMenuItem itemExit = new JMenuItem("Çıkış");

        itemNew.addActionListener(e -> newFile());
        itemOpen.addActionListener(e -> openFile());
        itemSave.addActionListener(e -> saveFile());
        itemSaveAs.addActionListener(e -> saveFileAs());
        itemExit.addActionListener(e -> System.exit(0));

        fileMenu.add(itemNew);
        fileMenu.add(itemOpen);
        fileMenu.add(itemSave);
        fileMenu.add(itemSaveAs);
        fileMenu.addSeparator();
        fileMenu.add(itemExit);

        JMenu runMenu = new JMenu("Çalıştır");
        JMenuItem itemRun = new JMenuItem("Çalıştır (F5)");
        itemRun.addActionListener(e -> runCode());
        runMenu.add(itemRun);

        menuBar.add(fileMenu);
        menuBar.add(runMenu);
        setJMenuBar(menuBar);
    }

    private void setupShortcuts() {
        InputMap im = editPane.getInputMap(JComponent.WHEN_FOCUSED);
        ActionMap am = editPane.getActionMap();

        // F5 - Çalıştır
        im.put(KeyStroke.getKeyStroke(KeyEvent.VK_F5, 0), "runCode");
        am.put("runCode", new AbstractAction() {
            public void actionPerformed(ActionEvent e) { runCode(); }
        });

        // Ctrl + S - Kaydet
        im.put(KeyStroke.getKeyStroke(KeyEvent.VK_S, InputEvent.CTRL_DOWN_MASK), "saveFile");
        am.put("saveFile", new AbstractAction() {
            public void actionPerformed(ActionEvent e) { saveFile(); }
        });

        // Ctrl + N - Yeni
        im.put(KeyStroke.getKeyStroke(KeyEvent.VK_N, InputEvent.CTRL_DOWN_MASK), "newFile");
        am.put("newFile", new AbstractAction() {
            public void actionPerformed(ActionEvent e) { newFile(); }
        });
    }

    private void setupDragAndDrop() {
        new DropTarget(editPane, DnDConstants.ACTION_COPY, new DropTargetAdapter() {
            @SuppressWarnings("unchecked")
            public void drop(DropTargetDropEvent dtde) {
                try {
                    dtde.acceptDrop(DnDConstants.ACTION_COPY);
                    List<File> droppedFiles = (List<File>) dtde.getTransferable().getTransferData(DataFlavor.javaFileListFlavor);
                    if (!droppedFiles.isEmpty()) {
                        loadFile(droppedFiles.get(0));
                    }
                } catch (Exception ex) {
                    ex.printStackTrace();
                }
            }
        });
    }

    private void newFile() {
        editPane.setText("");
        currentFile = null;
    }

    private void openFile() {
        JFileChooser chooser = new JFileChooser();
        if (chooser.showOpenDialog(this) == JFileChooser.APPROVE_OPTION) {
            loadFile(chooser.getSelectedFile());
        }
    }

    private void loadFile(File file) {
        try {
            String content = Files.readString(file.toPath(), StandardCharsets.UTF_8);
            editPane.setText(content);
            currentFile = file;
            runHighlight();
        } catch (IOException ex) {
            JOptionPane.showMessageDialog(this, "Dosya okuma hatası!", "Hata", JOptionPane.ERROR_MESSAGE);
        }
    }

    private boolean saveFile() {
        if (currentFile == null) {
            return saveFileAs();
        } else {
            try {
                Files.writeString(currentFile.toPath(), editPane.getText(), StandardCharsets.UTF_8);
                return true;
            } catch (IOException ex) {
                JOptionPane.showMessageDialog(this, "Dosya kaydetme hatası!", "Hata", JOptionPane.ERROR_MESSAGE);
                return false;
            }
        }
    }

    private boolean saveFileAs() {
        JFileChooser chooser = new JFileChooser();
        if (chooser.showSaveDialog(this) == JFileChooser.APPROVE_OPTION) {
            currentFile = chooser.getSelectedFile();
            return saveFile();
        }
        return false;
    }

    private void runCode() {
        if (saveFile() && currentFile != null) {
            // c++compiler.cpp mantığını arka planda çağırmak için ProcessBuilder
            try {
                String filePath = currentFile.getAbsolutePath();
                // Örnek: C++ derleyici modülünü çalıştırma
                ProcessBuilder pb = new ProcessBuilder("cmd.exe", "/c", "start", "cmd.exe", "/k", "g++ \"" + filePath + "\" -o run.exe && run.exe");
                pb.start();
            } catch (IOException ex) {
                JOptionPane.showMessageDialog(this, "Çalıştırma hatası!", "Hata", JOptionPane.ERROR_MESSAGE);
            }
        }
    }

    public static void main(String[] args) {
        String initialFile = (args.length > 0) ? args[0] : null;
        SwingUtilities.invokeLater(() -> {
            new OptivonEditor(initialFile).setVisible(true);
        });
    }
}