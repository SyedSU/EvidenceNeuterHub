using System.Drawing.Drawing2D;
using System.ComponentModel;
namespace EvidenceHub;
sealed class LayeredCanvas:FlowLayoutPanel
{
 [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool Light {get;set;}
 [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int Wash {get;set;}
 public LayeredCanvas(){DoubleBuffered=true;ResizeRedraw=true;}
 protected override void OnPaintBackground(PaintEventArgs e){if(ClientSize.Width<1||ClientSize.Height<1)return;using var brush=new LinearGradientBrush(ClientRectangle,BackColor,Light?Color.FromArgb(225,231,218):Color.FromArgb(25+Wash,42+Wash,34+Wash),35f);e.Graphics.FillRectangle(brush,ClientRectangle);using var line=new Pen(Color.FromArgb(55,194,167,111));e.Graphics.DrawLine(line,0,0,ClientSize.Width,0);}
}
sealed class LayeredButton:Button
{
 bool hover;
 [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public Color Accent {get;set;}=Color.FromArgb(194,167,111);
 public LayeredButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;DoubleBuffered=true;Cursor=Cursors.Hand;}
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
 protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var bounds=ClientRectangle;bounds.Inflate(-1,-1);if(bounds.Width<1||bounds.Height<1)return;using var glaze=new LinearGradientBrush(bounds,Color.FromArgb(hover&&Enabled?28:12,195,213,181),Color.Transparent,90f);e.Graphics.FillRectangle(glaze,bounds);using var edge=new Pen(Focused?Accent:Color.FromArgb(hover&&Enabled?170:65,Accent),Focused?2:1);e.Graphics.DrawRectangle(edge,bounds);if(Focused){bounds.Inflate(-5,-5);ControlPaint.DrawFocusRectangle(e.Graphics,bounds,ForeColor,BackColor);}}
}
sealed class LayeredSurface:Panel
{
 public LayeredSurface(){DoubleBuffered=true;ResizeRedraw=true;Padding=new Padding(16);}
 protected override void OnPaintBackground(PaintEventArgs e){base.OnPaintBackground(e);if(Width<4||Height<4)return;using var glaze=new LinearGradientBrush(ClientRectangle,Color.FromArgb(16,195,213,181),Color.FromArgb(4,194,167,111),90f);e.Graphics.FillRectangle(glaze,ClientRectangle);using var edge=new Pen(Color.FromArgb(85,194,167,111));e.Graphics.DrawRectangle(edge,1,1,Width-3,Height-3);using var highlight=new Pen(Color.FromArgb(35,238,238,230));e.Graphics.DrawLine(highlight,3,3,Width-4,3);}
}
