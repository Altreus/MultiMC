using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Gtk;
using Glade;

using MultiMC.GUI;
using MultiMC.Tasks;

namespace MultiMC.GTKGUI
{
	public class TextInputDialog : GTKDialog, ITextInputDialog
	{
        [Widget]
		VBox vboxTextInput = null;

        [Widget]
        Label textMessage = null;

        [Widget]
        Entry textInput = null;

		public TextInputDialog(string message = "", string text = "")
		{
			XML gxml = new XML(null, "MultiMC.GTKGUI.TextInputDialog.glade",
				"vboxTextInput", null);
			gxml.Autoconnect(this);

			this.VBox.PackStart(vboxTextInput);

			WidthRequest = 400;
			HeightRequest = 110;

			AddButton("_Cancel", ResponseType.Cancel);
			AddButton("_OK", ResponseType.Ok);

            textMessage.Text = message;
            textInput.Text = text;
		}

		public string Message
		{
			get { return textMessage.Text; }
			set { textMessage.Text = value; }
		}

		public string Input
		{
			get {
                Console.WriteLine(textInput.Text == null ? "NULL" : textInput.Text);
                return textInput.Text; }
			set { textInput.Text = value; }
		}

        public void HighlightText()
        {
            textInput.SelectRegion(0,textInput.Text.Length - 1);
        }
	}
}
