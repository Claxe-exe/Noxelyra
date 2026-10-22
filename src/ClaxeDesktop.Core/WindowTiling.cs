namespace ClaxeDesktop.Core;
public sealed record PixelRect(int X,int Y,int Width,int Height);
public static class WindowTiling
{
    public static IReadOnlyList<PixelRect> Resolve(MonitorInfo monitor,string template,int count,int gapDip=8)
    {
        var columns=template switch{"halves"=>2,"thirds"=>3,"grid"=>2,_=>throw new InvalidDataException("Yerleşim şablonu geçersiz.")};var rows=template=="grid"?2:1;
        if(count<1||count>columns*rows||gapDip is <0 or >32||!double.IsFinite(monitor.Scale)||monitor.Scale<=0)throw new InvalidDataException("Seçilen pencere sayısı şablona uymuyor.");
        var gap=(int)Math.Round(gapDip*monitor.Scale);var cellWidth=(monitor.Width-gap*(columns+1))/columns;var cellHeight=(monitor.Height-gap*(rows+1))/rows;
        if(cellWidth<160||cellHeight<100)throw new InvalidDataException("Bu ekranda şablon için yeterli alan yok.");
        return Enumerable.Range(0,count).Select(i=>new PixelRect(monitor.X+gap+(i%columns)*(cellWidth+gap),monitor.Y+gap+(i/columns)*(cellHeight+gap),cellWidth,cellHeight)).ToArray();
    }
}
