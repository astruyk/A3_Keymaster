using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Keymaster
{
	class DelegateCommand : ICommand
	{
		private Action<object> _action;
		private Func<object, bool> _func;

		public DelegateCommand(Action<object> action, Func<object, bool> func)
		{
			_action = action;
			_func = func;
		}

		public DelegateCommand(Action<object> action) : this(action, null)
		{
		}

		public bool CanExecute(object parameter)
		{
			if (_func == null) { return true; }
			return _func(parameter);
		}

		public event EventHandler CanExecuteChanged;

		public void Execute(object parameter)
		{
			_action(parameter);
		}
	}
}
