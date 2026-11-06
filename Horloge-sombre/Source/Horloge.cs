using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;

namespace HorlogeWindows {
public class Alarm {
 public string Id=Guid.NewGuid().ToString(), Name="Alarme", Time="07:00", Days="", Sound="Radar";
 public bool Enabled=true; public DateTime Next;
}
public class TimerItem {
 public string Name; public double Duration, Remaining; public DateTime End; public bool Running;
}
public class City {
 public string Name, Zone; public City(string n,string z){Name=n;Zone=z;}
}
public class AppWindow : Window { public static int Verify(){
  AppWindow w=null;
  try{
   new Application(); w=new AppWindow(); w.Show(); w.ticker.Stop();
   w.dataPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification-settings.xml");
   w.alarms.Clear();w.timers.Clear();w.recent.Clear();
   foreach(string page in new[]{"Chronomètre","Minuteurs","Alarmes","Horloges du monde"})w.ShowPage(page);
   w.ShowPage("Chronomètre");w.ToggleWatch();System.Threading.Thread.Sleep(25);w.Lap();w.ToggleWatch();
   if(w.laps.Count!=1||w.laps[0]<=0)throw new Exception("stopwatch lap");w.Lap();
   if(w.watch.ElapsedMilliseconds!=0||w.laps.Count!=0)throw new Exception("stopwatch reset");
   w.StartTimer(2,"Test");w.timers[0].End=DateTime.Now.AddSeconds(-1);w.Tick(null,EventArgs.Empty);
   if(w.timers.Count!=0||w.ringing==null)throw new Exception("timer expiration");w.ringing.Close();
   var a=new Alarm{Name="Test",Time="07:00",Next=DateTime.Now.AddSeconds(-1)};w.alarms.Add(a);w.Tick(null,EventArgs.Empty);
   if(a.Enabled||w.ringing==null)throw new Exception("alarm expiration");w.ringing.Close();
   w.Save();if(!File.Exists(w.dataPath))throw new Exception("persistence");
   w.alarms.Clear();w.Load();if(w.alarms.Count!=1||w.alarms[0].Enabled)throw new Exception("persistence roundtrip");
   File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification.txt"),"PASS: all four views, stopwatch start/lap/stop/reset, timer expiration and alert, one-shot alarm expiration and alert, XML persistence roundtrip.");
   w.Close();return 0;
  }catch(Exception ex){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification.txt"),ex.ToString());return 3;}
 }
 static readonly Brush Orange=Hex("#FF9F0A"), Muted=Hex("#98989D"), Surface=Hex("#1C1C1E"), Line=Hex("#303033");
 [DllImport("dwmapi.dll", PreserveSig=true)]
 static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
 static void DarkTitleBar(Window window){
  window.SourceInitialized+=(sender,args)=>{
   try{
    var handle=new WindowInteropHelper(window).Handle;
    int enabled=1;
    if(DwmSetWindowAttribute(handle,20,ref enabled,sizeof(int))!=0)
     DwmSetWindowAttribute(handle,19,ref enabled,sizeof(int));
    int black=0;
    DwmSetWindowAttribute(handle,35,ref black,sizeof(int));
   }catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
  };
 }
 static Brush Hex(string s){return new SolidColorBrush((Color)ColorConverter.ConvertFromString(s));}
 List<Alarm> alarms=new List<Alarm>(); List<TimerItem> timers=new List<TimerItem>();
 List<double> recent=new List<double>(); List<City> cities=new List<City>();
 static readonly City[] Catalog={new City("Paris","Romance Standard Time"),new City("Londres","GMT Standard Time"),new City("New York","Eastern Standard Time"),new City("Los Angeles","Pacific Standard Time"),new City("Tokyo","Tokyo Standard Time"),new City("Sydney","AUS Eastern Standard Time"),new City("Dubaï","Arabian Standard Time"),new City("Montréal","Eastern Standard Time"),new City("Singapour","Singapore Standard Time"),new City("Berlin","W. Europe Standard Time"),new City("Rome","W. Europe Standard Time"),new City("Pékin","China Standard Time"),new City("Mumbai","India Standard Time"),new City("Séoul","Korea Standard Time")};
 Stopwatch watch=new Stopwatch(); List<double> laps=new List<double>(); double lastLap;
 StackPanel content, nav; TextBlock watchDisplay, liveDate; Button watchStart, lapButton; StackPanel lapList;
 string page="Chronomètre"; DispatcherTimer ticker=new DispatcherTimer(); DateTime lastWall=DateTime.Now;
 string dataPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HorlogeWindows","settings.xml");
 Window ringing; List<Alarm> ringingAlarms=new List<Alarm>(); List<TimerItem> ringingTimers=new List<TimerItem>();
 SoundPlayer player; MemoryStream soundStream; string activeSound="Radar"; DateTime lastSound=DateTime.MinValue;
 public AppWindow(){
  Title="Horloge"; Width=460; Height=820; MinWidth=400; MinHeight=620; Background=Brushes.Black; Foreground=Brushes.White;
  DarkTitleBar(this);
  FontFamily=new FontFamily("Segoe UI"); WindowStartupLocation=WindowStartupLocation.CenterScreen;
  Load();
  var root=new Grid();root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(75)});Content=root;
  var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};root.Children.Add(scroll);
  content=new StackPanel{Margin=new Thickness(22,24,22,28),MaxWidth=520};scroll.Content=content;
  var bar=new Border{Background=Hex("#161618"),BorderBrush=Line,BorderThickness=new Thickness(0,1,0,0)};Grid.SetRow(bar,1);root.Children.Add(bar);
  nav=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};bar.Child=nav;liveDate=Text("",12,Muted);
  ShowPage(page); ticker.Interval=TimeSpan.FromMilliseconds(40);ticker.Tick+=Tick;ticker.Start();
  PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Space && page=="Chronomètre" && !(Keyboard.FocusedElement is TextBox) && ringing==null){ToggleWatch();e.Handled=true;}};
  Closing+=(s,e)=>{if(timers.Any(t=>t.Running)||alarms.Any(a=>a.Enabled)){if(MessageBox.Show(this,"Les alarmes et minuteurs ne sonneront plus après la fermeture. Quitter ?","Fermer Horloge",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes){e.Cancel=true;return;}}Save();if(player!=null)player.Stop();if(ringing!=null)ringing.Close();};
 }
 static TextBlock Text(string value,double size,Brush color){return new TextBlock{Text=value,FontSize=size,Foreground=color,TextWrapping=TextWrapping.Wrap};}
 Button Btn(string text,Action action,Brush bg=null,Brush fg=null){var b=new Button{Content=text,Background=bg??Surface,Foreground=fg??Brushes.White,BorderThickness=new Thickness(0),Padding=new Thickness(18,11,18,11),Margin=new Thickness(0,4,8,4),Cursor=Cursors.Hand,FontSize=14};
  var template=new ControlTemplate(typeof(Button));var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(40));border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Button.BackgroundProperty));var cp=new FrameworkElementFactory(typeof(ContentPresenter));cp.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Center);cp.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center);cp.SetValue(FrameworkElement.MarginProperty,new TemplateBindingExtension(Button.PaddingProperty));border.AppendChild(cp);template.VisualTree=border;b.Template=template;b.Click+=(s,e)=>action();return b;}
 Border Card(UIElement child){return new Border{Background=Brushes.Transparent,BorderBrush=Line,BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(0,15,0,15),Margin=new Thickness(0),Child=child};}
 StackPanel Row(){return new StackPanel{Orientation=Orientation.Horizontal};}
 void Header(string title,string subtitle,Action add){var g=new Grid{Margin=new Thickness(0,0,0,28)};var labels=new StackPanel();labels.Children.Add(Text(title,34,Brushes.White));g.Children.Add(labels);if(add!=null){var b=Btn("＋",add,Brushes.Transparent,Orange);b.FontSize=25;b.HorizontalAlignment=HorizontalAlignment.Right;b.VerticalAlignment=VerticalAlignment.Top;g.Children.Add(b);}content.Children.Add(g);}
 void ShowPage(string p){page=p;content.Children.Clear();nav.Children.Clear();watchDisplay=null;
  string[] names={"Horloges du monde","Alarmes","Chronomètre","Minuteurs"};string[] icons={"◎","♧","◴","◷"};string[] shortNames={"Horloge mondiale","Alarme","Chronomètre","Minuteurs"};
  for(int i=0;i<names.Length;i++){string name=names[i];var b=Btn(icons[i]+"\n"+shortNames[i],()=>ShowPage(name),Brushes.Transparent,name==p?Orange:Muted);b.Width=94;b.Height=65;b.FontSize=11;b.Padding=new Thickness(0);b.Margin=new Thickness(0);nav.Children.Add(b);}
  if(p=="Chronomètre")BuildWatch();else if(p=="Minuteurs")BuildTimers();else if(p=="Alarmes")BuildAlarms();else BuildWorld();
 }
 static string Duration(double ms,bool hundredths){var t=TimeSpan.FromMilliseconds(Math.Max(0,ms));return (t.TotalHours>=1?((int)t.TotalHours).ToString("00")+":":"")+t.Minutes.ToString("00")+":"+t.Seconds.ToString("00")+(hundredths?","+(t.Milliseconds/10).ToString("00"):"");}
 void BuildWatch(){Header("Chronomètre","Chaque instant compte.",null);watchDisplay=Text(Duration(watch.Elapsed.TotalMilliseconds,true),72,Brushes.White);watchDisplay.FontFamily=new FontFamily("Segoe UI Light");watchDisplay.TextAlignment=TextAlignment.Center;watchDisplay.Margin=new Thickness(0,105,0,95);content.Children.Add(watchDisplay);
  var controls=new Grid{Margin=new Thickness(8,0,0,34)};lapButton=Btn(watch.IsRunning?"Tour":"Réinitialiser",Lap);lapButton.HorizontalAlignment=HorizontalAlignment.Left;lapButton.Width=80;lapButton.Height=80;controls.Children.Add(lapButton);watchStart=Btn(watch.IsRunning?"Arrêter":"Démarrer",ToggleWatch,watch.IsRunning?Hex("#3C1315"):Hex("#09351A"),watch.IsRunning?Hex("#FF453A"):Hex("#30D158"));watchStart.HorizontalAlignment=HorizontalAlignment.Right;watchStart.Width=80;watchStart.Height=80;controls.Children.Add(watchStart);content.Children.Add(controls);
  lapList=new StackPanel();content.Children.Add(lapList);RenderLaps();}
 void ToggleWatch(){if(watch.IsRunning)watch.Stop();else watch.Start();if(watchStart!=null){watchStart.Content=watch.IsRunning?"Arrêter":"Démarrer";watchStart.Background=watch.IsRunning?Hex("#3C1315"):Hex("#09351A");watchStart.Foreground=watch.IsRunning?Hex("#FF453A"):Hex("#30D158");lapButton.Content=watch.IsRunning?"Tour":"Réinitialiser";}}
 void Lap(){if(watch.IsRunning){double now=watch.Elapsed.TotalMilliseconds;laps.Add(now-lastLap);lastLap=now;}else{watch.Reset();laps.Clear();lastLap=0;}RenderLaps();}
 void RenderLaps(){if(lapList==null)return;lapList.Children.Clear();if(laps.Count==0&&!watch.IsRunning){return;}if(watch.IsRunning)AddLap("Tour "+(laps.Count+1),Duration(watch.Elapsed.TotalMilliseconds-lastLap,true),Muted);for(int i=laps.Count-1;i>=0;i--){Brush c=laps.Count>1?(laps[i]==laps.Min()?Hex("#30D158"):laps[i]==laps.Max()?Hex("#FF453A"):Brushes.White):Brushes.White;AddLap("Tour "+(i+1),Duration(laps[i],true),c);}}
 void AddLap(string name,string val,Brush color){var g=new Grid{Margin=new Thickness(0,0,0,0)};g.Children.Add(Text(name,16,color));var t=Text(val,20,color);t.HorizontalAlignment=HorizontalAlignment.Right;g.Children.Add(t);lapList.Children.Add(new Border{BorderBrush=Line,BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(0,12,0,12),Child=g});}
 void BuildTimers(){Header("Minuteurs","Du temps pour ce qui compte.",()=>TimerDialog());
  if(timers.Count==0){
   var picker=new Grid{Margin=new Thickness(0,35,0,30)};for(int i=0;i<3;i++)picker.ColumnDefinitions.Add(new ColumnDefinition());
   var hours=Select(Enumerable.Range(0,24).Select(n=>n.ToString("00")),"00");var mins=Select(Enumerable.Range(0,60).Select(n=>n.ToString("00")),"05");var secs=Select(Enumerable.Range(0,60).Select(n=>n.ToString("00")),"00");
   var choices=new[]{hours,mins,secs};var units=new[]{"heures","min","s"};for(int i=0;i<3;i++){var part=new StackPanel{Margin=new Thickness(3)};part.Children.Add(choices[i]);var unit=Text(units[i],13,Muted);unit.TextAlignment=TextAlignment.Center;part.Children.Add(unit);Grid.SetColumn(part,i);picker.Children.Add(part);}content.Children.Add(picker);
   var controls=new Grid{Margin=new Thickness(0,20,0,45)};var cancel=Btn("Annuler",()=>{},Hex("#242426"),Muted);cancel.Width=80;cancel.Height=80;cancel.HorizontalAlignment=HorizontalAlignment.Left;controls.Children.Add(cancel);var start=Btn("Démarrer",()=>{int h=int.Parse((string)hours.SelectedItem),m=int.Parse((string)mins.SelectedItem),sec=int.Parse((string)secs.SelectedItem);StartTimer(h*3600+m*60+sec,"Minuteur");},Hex("#09351A"),Hex("#30D158"));start.Width=80;start.Height=80;start.HorizontalAlignment=HorizontalAlignment.Right;controls.Children.Add(start);content.Children.Add(controls);
   var sound=new Grid{Margin=new Thickness(0,0,0,25)};sound.Children.Add(Text("Sonnerie",17,Brushes.White));var label=Text("Radar  ›",17,Muted);label.HorizontalAlignment=HorizontalAlignment.Right;sound.Children.Add(label);content.Children.Add(new Border{BorderBrush=Line,BorderThickness=new Thickness(0,1,0,1),Padding=new Thickness(0,15,0,15),Child=sound});
  }
  foreach(var timer in timers.ToList()){var t=timer;var stack=new StackPanel();stack.Children.Add(Text(t.Name,17,Muted));var display=Text(Duration(t.Running?(t.End-DateTime.Now).TotalMilliseconds:t.Remaining*1000,false),48,Brushes.White);display.Tag=t;stack.Children.Add(display);var progress=new ProgressBar{Minimum=0,Maximum=t.Duration,Value=Math.Max(0,t.Duration-t.Remaining),Height=3,Foreground=Orange,Background=Line,Margin=new Thickness(0,14,0,14),Tag=t};stack.Children.Add(progress);var buttons=Row();buttons.Children.Add(Btn(t.Running?"Pause":"Reprendre",()=>{if(t.Running){t.Remaining=Math.Max(0,(t.End-DateTime.Now).TotalSeconds);t.Running=false;}else{t.End=DateTime.Now.AddSeconds(t.Remaining);t.Running=true;}Save();BuildCurrent();},null,Orange));buttons.Children.Add(Btn("Annuler",()=>{timers.Remove(t);Save();BuildCurrent();}));stack.Children.Add(buttons);content.Children.Add(Card(stack));}
  if(recent.Count>0){content.Children.Add(Text("Récents",22,Brushes.White));var r=Row();foreach(double seconds in recent.Take(4)){double d=seconds;r.Children.Add(Btn(Duration(d*1000,false),()=>StartTimer(d,"Minuteur")));}content.Children.Add(r);}}
 void BuildCurrent(){ShowPage(page);}
 void StartTimer(double seconds,string name){if(seconds<=0)return;timers.Add(new TimerItem{Name=string.IsNullOrWhiteSpace(name)?"Minuteur":name,Duration=seconds,Remaining=seconds,Running=true,End=DateTime.Now.AddSeconds(seconds)});recent.Remove(seconds);recent.Insert(0,seconds);recent=recent.Take(8).ToList();Save();ShowPage("Minuteurs");}
 Window Dialog(string title,int height){var window=new Window{Title=title,Width=430,Height=height,ResizeMode=ResizeMode.NoResize,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Hex("#171719"),Foreground=Brushes.White,FontFamily=FontFamily};DarkTitleBar(window);return window;}
 TextBox Input(string value){return new TextBox{Text=value,FontSize=18,Background=Hex("#303033"),Foreground=Brushes.White,BorderThickness=new Thickness(0),Padding=new Thickness(10),Margin=new Thickness(0,5,0,14)};}
 static Style UiStyle(string xaml){return (Style)XamlReader.Parse(xaml);}
 static readonly Style DarkComboItem=UiStyle(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='{x:Type ComboBoxItem}' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Setter Property='Foreground' Value='White'/><Setter Property='Padding' Value='12,9'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ComboBoxItem}'><Border x:Name='bg' Background='Transparent' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='bg' Property='Background' Value='#444447'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter Property='Foreground' Value='#FF9F0A'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
 static readonly Style DarkCombo=UiStyle(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='{x:Type ComboBox}' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Setter Property='Foreground' Value='White'/><Setter Property='Background' Value='#2C2C2E'/><Setter Property='MinHeight' Value='44'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ComboBox}'><Grid>
   <ToggleButton x:Name='toggle' Foreground='White' Focusable='False' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'><ToggleButton.Template><ControlTemplate TargetType='{x:Type ToggleButton}'><Border Background='#2C2C2E' CornerRadius='9'><Grid><ContentPresenter Content='{Binding SelectionBoxItem, RelativeSource={RelativeSource AncestorType=ComboBox}}' Margin='12,0,28,0' VerticalAlignment='Center'/><TextBlock Text='⌄' Foreground='#98989D' FontSize='18' HorizontalAlignment='Right' Margin='0,0,12,2' VerticalAlignment='Center'/></Grid></Border></ControlTemplate></ToggleButton.Template></ToggleButton>
   <Popup x:Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False' PopupAnimation='Fade'><Border Background='#2C2C2E' BorderBrush='#48484A' BorderThickness='1' CornerRadius='9' MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}' MaxHeight='250'><ScrollViewer CanContentScroll='True' VerticalScrollBarVisibility='Auto'><ItemsPresenter/></ScrollViewer></Border></Popup>
  </Grid></ControlTemplate></Setter.Value></Setter></Style>");
 static readonly Style DayStyle=UiStyle(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='{x:Type CheckBox}' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Setter Property='Foreground' Value='White'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type CheckBox}'><Border x:Name='circle' Width='39' Height='39' CornerRadius='20' Background='#303033'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='circle' Property='Background' Value='#FF9F0A'/><Setter Property='Foreground' Value='Black'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
 ComboBox Select(IEnumerable<string> values,string chosen){var c=new ComboBox{Style=DarkCombo,ItemContainerStyle=DarkComboItem,FontSize=16,Margin=new Thickness(0,5,0,14)};foreach(string v in values)c.Items.Add(v);c.SelectedItem=chosen;if(c.SelectedIndex<0)c.SelectedIndex=0;return c;}
 void TimerDialog(){var w=Dialog("Nouveau minuteur",450);var s=new StackPanel{Margin=new Thickness(26)};w.Content=s;s.Children.Add(Text("Nouveau minuteur",26,Brushes.White));s.Children.Add(Text("Durée · heures : minutes : secondes",13,Muted));var duration=Input("00:05:00");s.Children.Add(duration);s.Children.Add(Text("Nom",13,Muted));var name=Input("Minuteur");s.Children.Add(name);var error=Text("",12,Hex("#FF453A"));s.Children.Add(error);s.Children.Add(Btn("Démarrer",()=>{TimeSpan ts;if(!TimeSpan.TryParseExact(duration.Text.Trim(),new[]{@"h\:mm\:ss",@"hh\:mm\:ss"},CultureInfo.InvariantCulture,out ts)||ts.TotalSeconds<=0||ts.TotalHours>=24){error.Text="Entrez une durée entre 00:00:01 et 23:59:59.";return;}StartTimer(ts.TotalSeconds,name.Text);w.Close();},Orange,Brushes.Black));w.ShowDialog();}
 void BuildAlarms(){Header("Alarmes","Commencez la journée à votre rythme.",()=>AlarmDialog(null));if(alarms.Count==0){var empty=new StackPanel();empty.Children.Add(Text("Aucune alarme",28,Brushes.White));empty.Children.Add(Text("Ajoutez votre premier réveil avec le bouton +.",14,Muted));content.Children.Add(Card(empty));}
  foreach(var a0 in alarms.OrderBy(a=>a.Time)){var a=a0;var g=new Grid();var s=new StackPanel();s.Children.Add(Text(a.Time,54,a.Enabled?Brushes.White:Muted));s.Children.Add(Text(a.Name+" · "+DayLabel(a.Days),14,Muted));if(a.Enabled)s.Children.Add(Text("Prochaine : "+a.Next.ToString("ddd d MMM, HH:mm",CultureInfo.GetCultureInfo("fr-FR")),12,Orange));var r=Row();r.Children.Add(Btn("Modifier",()=>AlarmDialog(a),null,Orange));r.Children.Add(Btn("Supprimer",()=>{alarms.Remove(a);Save();BuildCurrent();}));s.Children.Add(r);g.Children.Add(s);var toggle=Btn(a.Enabled?"●  Activée":"○  Désactivée",()=>{a.Enabled=!a.Enabled;if(a.Enabled)a.Next=NextAlarm(a,DateTime.Now);Save();BuildCurrent();},a.Enabled?Hex("#143C21"):Hex("#303033"),a.Enabled?Hex("#30D158"):Muted);toggle.HorizontalAlignment=HorizontalAlignment.Right;toggle.VerticalAlignment=VerticalAlignment.Top;g.Children.Add(toggle);content.Children.Add(Card(g));}}
 static string DayLabel(string days){if(days=="")return "Une seule fois";if(days.Length==7)return "Tous les jours";if(days=="12345")return "En semaine";string[] n={"dim.","lun.","mar.","mer.","jeu.","ven.","sam."};return string.Join(", ",days.Select(c=>n[c-'0']).ToArray());}
 public static DateTime NextAlarm(Alarm a,DateTime after){TimeSpan time=TimeSpan.ParseExact(a.Time,@"hh\:mm",CultureInfo.InvariantCulture);for(int i=0;i<8;i++){var d=after.Date.AddDays(i).Add(time);if(d>after&&(a.Days.Length==0||a.Days.Contains(((int)d.DayOfWeek).ToString())))return d;}return after.Date.AddDays(1).Add(time);}
 void AlarmDialog(Alarm edit){var w=Dialog(edit==null?"Ajouter une alarme":"Modifier l’alarme",620);var s=new StackPanel{Margin=new Thickness(26)};w.Content=s;s.Children.Add(Text(edit==null?"Nouvelle alarme":"Modifier l’alarme",26,Brushes.White));s.Children.Add(Text("Heure · format 24 h",13,Muted));var time=Input(edit==null?"07:00":edit.Time);s.Children.Add(time);s.Children.Add(Text("Nom",13,Muted));var name=Input(edit==null?"Réveil":edit.Name);s.Children.Add(name);s.Children.Add(Text("Répéter · aucun jour = une seule fois",13,Muted));var days=Row();var checks=new List<CheckBox>();string[] labels={"L","M","M","J","V","S","D"};for(int i=0;i<7;i++){int d=(i+1)%7;var c=new CheckBox{Content=labels[i],Tag=d,IsChecked=edit!=null&&edit.Days.Contains(d.ToString()),Style=DayStyle,Margin=new Thickness(0,12,5,18)};checks.Add(c);days.Children.Add(c);}s.Children.Add(days);s.Children.Add(Text("Sonnerie",13,Muted));var sound=Select(new[]{"Radar","Signal","Carillon"},edit==null?"Radar":edit.Sound);s.Children.Add(sound);s.Children.Add(Btn("Écouter",()=>PlayTone((string)sound.SelectedItem),null,Orange));var error=Text("",12,Hex("#FF453A"));s.Children.Add(error);s.Children.Add(Btn("Enregistrer",()=>{TimeSpan ts;if(!TimeSpan.TryParseExact(time.Text.Trim(),@"hh\:mm",CultureInfo.InvariantCulture,out ts)){error.Text="Entrez une heure valide, par exemple 07:30.";return;}var a=edit??new Alarm();a.Time=ts.ToString(@"hh\:mm");a.Name=string.IsNullOrWhiteSpace(name.Text)?"Alarme":name.Text.Trim();a.Days=string.Concat(checks.Where(c=>c.IsChecked==true).Select(c=>c.Tag.ToString()).OrderBy(x=>x));a.Sound=(string)sound.SelectedItem;a.Enabled=true;a.Next=NextAlarm(a,DateTime.Now);if(edit==null)alarms.Add(a);Save();BuildCurrent();w.Close();},Orange,Brushes.Black));w.ShowDialog();}
 void BuildWorld(){Header("Horloges du monde","Un même instant, partout dans le monde.",CityDialog);foreach(var c0 in cities){var c=c0;var s=new StackPanel();var g=new Grid();s.Children.Add(Text(c.Name,26,Brushes.White));var date=Text("",13,Muted);date.Tag=c;s.Children.Add(date);g.Children.Add(s);var right=new StackPanel{HorizontalAlignment=HorizontalAlignment.Right};var time=Text("",42,Brushes.White);time.Tag=c;right.Children.Add(time);right.Children.Add(Btn("Retirer",()=>{cities.Remove(c);Save();BuildCurrent();},null,Muted));g.Children.Add(right);content.Children.Add(Card(g));}UpdateWorld(content);}
 void CityDialog(){var w=Dialog("Ajouter une ville",260);var s=new StackPanel{Margin=new Thickness(26)};w.Content=s;s.Children.Add(Text("Ajouter une ville",26,Brushes.White));var select=Select(Catalog.Select(c=>c.Name),"Tokyo");s.Children.Add(select);s.Children.Add(Btn("Ajouter",()=>{var city=Catalog.First(c=>c.Name==(string)select.SelectedItem);if(!cities.Any(c=>c.Name==city.Name))cities.Add(new City(city.Name,city.Zone));Save();BuildCurrent();w.Close();},Orange,Brushes.Black));w.ShowDialog();}
 void UpdateWorld(DependencyObject obj){foreach(var child in Children(obj)){var text=child as TextBlock;if(text!=null&&text.Tag is City){var c=(City)text.Tag;try{var local=TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.Now,c.Zone);if(text.FontSize>30)text.Text=local.ToString("HH:mm:ss");else{var diff=local.Date-DateTime.Today;text.Text=(diff.Days<0?"Hier":diff.Days>0?"Demain":"Aujourd’hui")+" · "+local.ToString("ddd d MMM",CultureInfo.GetCultureInfo("fr-FR"));}}catch(TimeZoneNotFoundException){text.Text="Fuseau indisponible";}}UpdateWorld(child);}}
 static IEnumerable<DependencyObject> Children(DependencyObject o){int n=VisualTreeHelper.GetChildrenCount(o);for(int i=0;i<n;i++)yield return VisualTreeHelper.GetChild(o,i);}
 void UpdateTimerViews(DependencyObject o){foreach(var child in Children(o)){var text=child as TextBlock;if(text!=null&&text.Tag is TimerItem){var t=(TimerItem)text.Tag;text.Text=Duration((t.Running?Math.Max(0,(t.End-DateTime.Now).TotalSeconds):t.Remaining)*1000,false);}var bar=child as ProgressBar;if(bar!=null&&bar.Tag is TimerItem){var t=(TimerItem)bar.Tag;bar.Value=Math.Max(0,t.Duration-(t.Running?(t.End-DateTime.Now).TotalSeconds:t.Remaining));}UpdateTimerViews(child);}}
 void Tick(object sender,EventArgs e){var now=DateTime.Now;liveDate.Text=now.ToString("ddd d MMM · HH:mm",CultureInfo.GetCultureInfo("fr-FR"));if(watchDisplay!=null){watchDisplay.Text=Duration(watch.Elapsed.TotalMilliseconds,true);if(watch.IsRunning)RenderLaps();}if(page=="Minuteurs")UpdateTimerViews(content);if(page=="Horloges du monde")UpdateWorld(content);
  bool changed=false;foreach(var t in timers.Where(t=>t.Running&&t.End<=now).ToList()){t.Running=false;t.Remaining=0;timers.Remove(t);ringingTimers.Add(t);changed=true;}
  foreach(var a in alarms.Where(a=>a.Enabled&&a.Next<=now).ToList()){ringingAlarms.Add(a);activeSound=a.Sound;if(a.Days.Length==0)a.Enabled=false;else a.Next=NextAlarm(a,now);changed=true;}
  if(changed){Save();BuildCurrent();ShowRing();}if(ringing!=null&&(now-lastSound).TotalSeconds>=2.4){PlayTone(activeSound);lastSound=now;}lastWall=now;
 }
 void ShowRing(){if(ringing!=null){UpdateRing();return;}ringing=Dialog("C’est l’heure",380);ringing.Topmost=true;ringing.Closing+=(s,e)=>{if(player!=null)player.Stop();ringing=null;ringingAlarms.Clear();ringingTimers.Clear();};UpdateRing();ringing.Show();ringing.Activate();}
 void UpdateRing(){var s=new StackPanel{Margin=new Thickness(26)};ringing.Content=s;s.Children.Add(Text("◷  C’est l’heure",30,Orange));s.Children.Add(Text(string.Join("\n",ringingAlarms.Select(a=>a.Name).Concat(ringingTimers.Select(t=>t.Name)).ToArray()),23,Brushes.White));if(ringingAlarms.Count>0)s.Children.Add(Btn("Rappel dans 9 minutes",()=>{foreach(var a in ringingAlarms){a.Enabled=true;a.Next=DateTime.Now.AddMinutes(9);}Save();BuildCurrent();ringing.Close();},Orange,Brushes.Black));s.Children.Add(Btn("Arrêter",()=>ringing.Close()));}
 void PlayTone(string sound){try{if(player!=null)player.Stop();if(soundStream!=null)soundStream.Dispose();int rate=22050;double seconds=1.65;int count=(int)(rate*seconds);soundStream=new MemoryStream();var w=new BinaryWriter(soundStream);w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(Encoding.ASCII.GetBytes("data"));w.Write(count*2);for(int i=0;i<count;i++){double t=(double)i/rate;double f=sound=="Carillon"?660+220*((int)(t*3)%3):sound=="Signal"?880:740+140*((int)(t*6)%2);double phase=t%0.32;double env=Math.Min(1,phase*80)*Math.Exp(-phase*9);double sample=Math.Sin(2*Math.PI*f*t)*env*0.27;w.Write((short)(sample*short.MaxValue));}w.Flush();soundStream.Position=0;player=new SoundPlayer(soundStream);player.Play();}catch{SystemSounds.Exclamation.Play();}}
 void Save(){try{var root=new XElement("horloge",new XElement("alarms",alarms.Select(a=>new XElement("alarm",new XAttribute("id",a.Id),new XAttribute("name",a.Name),new XAttribute("time",a.Time),new XAttribute("days",a.Days),new XAttribute("sound",a.Sound),new XAttribute("enabled",a.Enabled),new XAttribute("next",a.Next.ToString("o"))))),new XElement("timers",timers.Select(t=>new XElement("timer",new XAttribute("name",t.Name),new XAttribute("duration",t.Duration),new XAttribute("remaining",t.Remaining),new XAttribute("running",t.Running),new XAttribute("end",t.End.ToString("o"))))),new XElement("recent",recent.Select(d=>new XElement("duration",d))),new XElement("cities",cities.Select(c=>new XElement("city",new XAttribute("name",c.Name),new XAttribute("zone",c.Zone)))));Directory.CreateDirectory(Path.GetDirectoryName(dataPath));string tmp=dataPath+".tmp";root.Save(tmp);if(File.Exists(dataPath))File.Replace(tmp,dataPath,null);else File.Move(tmp,dataPath);}catch(Exception ex){Debug.WriteLine(ex.Message);}}
 void Load(){try{if(File.Exists(dataPath)){var r=XElement.Load(dataPath);foreach(var x in r.Element("alarms").Elements()){var a=new Alarm{Id=(string)x.Attribute("id"),Name=(string)x.Attribute("name"),Time=(string)x.Attribute("time"),Days=(string)x.Attribute("days"),Sound=(string)x.Attribute("sound"),Enabled=(bool)x.Attribute("enabled"),Next=(DateTime)x.Attribute("next")};TimeSpan.ParseExact(a.Time,@"hh\:mm",CultureInfo.InvariantCulture);if(a.Next<DateTime.Now)a.Next=NextAlarm(a,DateTime.Now);alarms.Add(a);}foreach(var x in r.Element("timers").Elements()){var t=new TimerItem{Name=(string)x.Attribute("name"),Duration=(double)x.Attribute("duration"),Remaining=(double)x.Attribute("remaining"),Running=(bool)x.Attribute("running"),End=(DateTime)x.Attribute("end")};if(!t.Running||t.End>DateTime.Now)timers.Add(t);}foreach(var x in r.Element("recent").Elements())recent.Add((double)x);foreach(var x in r.Element("cities").Elements())cities.Add(new City((string)x.Attribute("name"),(string)x.Attribute("zone")));return;}}catch(Exception ex){Debug.WriteLine(ex.Message);alarms.Clear();timers.Clear();cities.Clear();}cities.Add(Catalog[0]);cities.Add(Catalog[2]);cities.Add(Catalog[4]);}
 [STAThread]public static int Main(string[] args){if(args.Contains("--verify"))return Verify();if(args.Contains("--self-test")){try{var a=new Alarm{Time="07:00",Days="12345"};if(NextAlarm(a,new DateTime(2026,11,6,8,0,0))!=new DateTime(2026,11,9,7,0,0))throw new Exception("weekday");a.Days="";if(NextAlarm(a,new DateTime(2026,11,6,7,0,0))!=new DateTime(2026,11,7,7,0,0))throw new Exception("next day");if(Duration(61234,true)!="01:01,23")throw new Exception("duration");if(Duration(3661000,false)!="01:01:01")throw new Exception("hour");File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test.txt"),"PASS: weekday scheduling, next-day scheduling, stopwatch precision, hour formatting");return 0;}catch{return 1;}}try{var app=new Application();app.Run(new AppWindow());return 0;}catch(Exception ex){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"crash.txt"),ex.ToString());return 2;}}
}
}
