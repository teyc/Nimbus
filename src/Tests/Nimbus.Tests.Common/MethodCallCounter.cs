﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Nimbus.ConcurrentCollections;
using Nimbus.Extensions;

namespace Nimbus.Tests.Common
{
    public static class MethodCallCounter
    {
        private static readonly ThreadSafeDictionary<string, ConcurrentBag<object[]>> _allReceivedCalls = new ThreadSafeDictionary<string, ConcurrentBag<object[]>>();

        public static IEnumerable<KeyValuePair<string, ConcurrentBag<object[]>>> AllReceivedCalls
        {
            get { return _allReceivedCalls.ToDictionary(); }
        }

        public static void RecordCall<T>(Expression<Action<T>> expr)
        {
            var methodCallExpression = (MethodCallExpression) expr.Body;
            var method = methodCallExpression.Method;

            // http://stackoverflow.com/questions/2616638/access-the-value-of-a-member-expression
            var args = new List<object>();
            foreach (var argExpr in methodCallExpression.Arguments)
            {
                var messageExpression = argExpr;
                var objectMember = Expression.Convert(messageExpression, typeof (object));
                var getterLambda = Expression.Lambda<Func<object>>(objectMember);
                var getter = getterLambda.Compile();
                var arg = getter();
                args.Add(arg);
            }

            var key = GetMethodKey(typeof(T), method);
            var methodCallBag = _allReceivedCalls.GetOrAdd(key, k => new ConcurrentBag<object[]>());
            methodCallBag.Add(args.ToArray());

            Console.WriteLine("Observed call to {0}".FormatWith(key));
        }

        public static IEnumerable<object> AllReceivedMessages
        {
            get
            {
                var messageBags = _allReceivedCalls.ToDictionary().Values;

                var messages = messageBags
                    .SelectMany(c => c)
                    .SelectMany(args => args)
                    .ToArray();
                return messages;
            }
        }

        public static IEnumerable<object[]> ReceivedCallsWithAnyArg<T>(Expression<Action<T>> expr)
        {
            var methodCallExpression = (MethodCallExpression) expr.Body;
            var method = methodCallExpression.Method;
            var key = GetMethodKey(typeof(T), method);
            var messageBag = _allReceivedCalls.GetOrAdd(key, k => new ConcurrentBag<object[]>());
            return messageBag;
        }

        public static void Clear()
        {
            _allReceivedCalls.Clear();
        }

        private static string GetMethodKey(Type type, MethodInfo method)
        {
            var parameters = method
                .GetParameters()
                .Select(p => "{0} {1}".FormatWith(p.ParameterType, p.Name))
                .ToArray();

            var parameterString = string.Join(", ", parameters);

            var key = "{0}.{1}({2})".FormatWith(type.FullName, method.Name, parameterString);
            return key;
        }
    }
}