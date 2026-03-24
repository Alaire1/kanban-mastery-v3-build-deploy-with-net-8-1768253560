using System;
using System.ComponentModel.DataAnnotations;

namespace KanbanApi.Validation
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class NotEmptyOrWhitespaceAttribute : ValidationAttribute
    {
        public NotEmptyOrWhitespaceAttribute()
        {
            ErrorMessage = "Column name cannot be empty";
        }

        public override bool IsValid(object? value)
        {
            if (value is string str)
            {
                return !string.IsNullOrWhiteSpace(str);
            }
            return false;
        }
    }
}
