using System;
using System.Windows.Forms;
using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;

namespace IsaacSaveBackup
{
    public partial class Form1 : Form
    {
        private string _steampath;
        private string _lastsave;

        private class ComboBoxItem
        {
            public string Name { get; set; }
            public string ID { get; set; }
            
            public ComboBoxItem(string name = "", string id = "")
            {
                Name = name;
                ID = id;
            }

            public override string ToString()
            {
                return string.Format("{0} [{1}]", Name, ID);
            }
        }

        private class ListBoxItem
        {
            public string Text { get; set; }
            public string Tag { get; set; }

            public ListBoxItem(string text = "", string tag = "")
            {
                Text = text;
                Tag = tag;
            }

            public override string ToString()
            {
                return Text;
            }
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam");
            if (key == null)
            {
                MessageBox.Show("没有安装 Steam", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
            _steampath = key.GetValue("SteamPath") as string;
            foreach (var i in key.OpenSubKey("Users").GetSubKeyNames())
            {
                var p = Path.Combine(_steampath, "userdata", i, "config", "localconfig.vdf");
                if (File.Exists(p))
                {
                    var m = Regex.Match(File.ReadAllText(p), "\"PersonaName\"\t\t\"(.+)\"");
                    var name = m.Groups[1].Value;
                    if (Directory.Exists(Path.Combine(_steampath, "userdata", i, "250900")))
                    {
                        var index= comboBox1.Items.Add(new ComboBoxItem(name, i));
                    }
                }

            }
            if (comboBox1.Items.Count == 0)
            {
                MessageBox.Show("没有找到以撒存档目录，请先安装游戏，并至少运行一次。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
            comboBox1.SelectedIndex = 0;
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = comboBox1.SelectedItem as ComboBoxItem;
            fileSystemWatcher1.Path = Path.Combine(_steampath, "userdata", item.ID, "250900", "remote");
            fileSystemWatcher1.EnableRaisingEvents = true;
        }

        private void fileSystemWatcher1_Changed(object sender, FileSystemEventArgs e)
        {
            onFileChanged();
        }

        private void fileSystemWatcher1_Created(object sender, FileSystemEventArgs e)
        {
            onFileChanged();
        }

        private void onFileChanged()
        {
            Thread.Sleep(1000); // 延迟1秒执行，避免文件占用
            var now = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var dist = Path.Combine(Application.StartupPath, "save", now);
            if (Directory.Exists(dist))
            {
                return;
            }
            Directory.CreateDirectory(Path.Combine(dist, "remote"));
            var source = fileSystemWatcher1.Path;
            foreach (var i in Directory.GetFiles(source))
            {
                File.Copy(i, Path.Combine(dist, "remote", Path.GetFileName(i)));
            }
            File.Copy(Path.Combine(Directory.GetParent(source).FullName, "remotecache.vdf"), Path.Combine(dist, "remotecache.vdf"));
            _lastsave = dist;
            listBox1.Items.Add(new ListBoxItem(string.Format("[{0}] 存档备份已创建", now), dist));
        }

        private void fileSystemWatcher1_Deleted(object sender, FileSystemEventArgs e)
        {
            if (string.IsNullOrEmpty(_lastsave))
                return;

            var ret = MessageBox.Show("角色已死亡，是否还原到最近一次的备份？（这将自动关闭游戏）", "询问", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1, MessageBoxOptions.ServiceNotification);
            if (ret == DialogResult.Yes)
            {
                restoresave(_lastsave);
            }
        }

        private void restoresave(string dist)
        {
            fileSystemWatcher1.EnableRaisingEvents = false;
            foreach (var p in Process.GetProcessesByName("isaac-ng"))
            {
                p.Kill();
                p.WaitForExit();
            }
            foreach (var i in Directory.GetFiles(Path.Combine(dist, "remote")))
            {
                File.Copy(i, Path.Combine(fileSystemWatcher1.Path, Path.GetFileName(i)), true);
            }
            File.Copy(Path.Combine(dist, "remotecache.vdf"), Path.Combine(Directory.GetParent(fileSystemWatcher1.Path).FullName, "remotecache.vdf"), true);
            fileSystemWatcher1.EnableRaisingEvents = true;
            var ret = MessageBox.Show("已还原存档，是否启动游戏？", "询问", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ret == DialogResult.Yes)
            {
                Process.Start("steam://rungameid/250900");
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (button1.Text == "暂停监控")
            {
                fileSystemWatcher1.EnableRaisingEvents = false;
                button1.Text = "开始监控";
            }
            else
            {
                fileSystemWatcher1.EnableRaisingEvents = true;
                button1.Text = "暂停监控";
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var item = listBox1.SelectedItem as ListBoxItem;
            if (item == null)
            {
                MessageBox.Show("请选中想要还原的存档", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var ret = MessageBox.Show("确定要恢复到选中的备份吗？（这将自动关闭游戏）", "询问", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ret == DialogResult.Yes)
            {
                restoresave(item.Tag);
            }
        }

        private void 打开存档目录ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(Directory.GetParent(fileSystemWatcher1.Path).FullName);
        }

        private void 打开备份目录ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists(Path.Combine(Application.StartupPath, "save")))
            {
                MessageBox.Show("当前没有任何备份", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Process.Start(Path.Combine(Application.StartupPath, "save"));
        }

        private void 清空所有备份ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Directory.Delete(Path.Combine(Application.StartupPath, "save"), true);
            }
            catch (Exception)
            {
            }
            listBox1.Items.Clear();
            _lastsave = "";
        }
    }
}
