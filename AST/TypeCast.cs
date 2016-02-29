﻿using System;
using System.Diagnostics;
using CodeGeneration;

namespace AST {
    public enum TypeCastType {
        NOP,
        INT8_TO_INT16,
        INT8_TO_INT32,

        INT16_TO_INT32,

        INT32_TO_FLOAT,
        INT32_TO_DOUBLE,

        PRESERVE_INT8,
        PRESERVE_INT16,

        UINT8_TO_UINT16,
        UINT8_TO_UINT32,

        UINT16_TO_UINT32,

        FLOAT_TO_INT32,
        FLOAT_TO_DOUBLE,

        DOUBLE_TO_INT32,
        DOUBLE_TO_FLOAT
    }

    public sealed class TypeCast : Expr {
        private TypeCast(TypeCastType kind, Expr expr, ExprType type, Env env)
            : base(type) {
            this.Expr = expr;
            this.Kind = kind;
            this.Env = env;
        }

        public TypeCast(TypeCastType kind, Expr expr, ExprType type)
            : this(kind, expr, type, expr.Env) { }

        public readonly Expr Expr;
        public readonly TypeCastType Kind;

        // Note: typecast might introduce environment changes.
        public override Env Env { get; }

        // A typecast cannot be an lvalue.
        // int a;
        // (char)a = 'a'; // error: an lvalue is required.
        public override Boolean IsLValue => false;

        public override Reg CGenValue(Env env, CGenState state) {
            Reg ret = this.Expr.CGenValue(env, state);
            switch (this.Kind) {
                case TypeCastType.DOUBLE_TO_FLOAT:
                case TypeCastType.FLOAT_TO_DOUBLE:
                case TypeCastType.PRESERVE_INT16:
                case TypeCastType.PRESERVE_INT8:
                case TypeCastType.NOP:
                    return ret;

                case TypeCastType.DOUBLE_TO_INT32:
                case TypeCastType.FLOAT_TO_INT32:
                    state.CGenConvertFloatToLong();
                    return Reg.EAX;

                case TypeCastType.INT32_TO_DOUBLE:
                case TypeCastType.INT32_TO_FLOAT:
                    state.CGenConvertLongToFloat();
                    return Reg.ST0;

                case TypeCastType.INT16_TO_INT32:
                    state.MOVSWL(Reg.AX, Reg.EAX);
                    return ret;

                case TypeCastType.INT8_TO_INT16:
                case TypeCastType.INT8_TO_INT32:
                    state.MOVSBL(Reg.AL, Reg.EAX);
                    return ret;

                case TypeCastType.UINT16_TO_UINT32:
                    state.MOVZWL(Reg.AX, Reg.EAX);
                    return ret;

                case TypeCastType.UINT8_TO_UINT16:
                case TypeCastType.UINT8_TO_UINT32:
                    state.MOVZBL(Reg.AL, Reg.EAX);
                    return ret;

                default:
                    throw new InvalidProgramException();
            }
        }

        public static Boolean EqualType(ExprType t1, ExprType t2) {
            return t1.EqualType(t2);
        }

        /// <summary>
        /// From:
        ///     char, short, long
        /// To:
        ///     char, uchar, short, ushort, long, ulong, float double
        /// </summary>
        public static Expr SignedIntegralToArith(Expr expr, ExprType type) {
            ExprTypeKind from = expr.Type.Kind;
            ExprTypeKind to = type.Kind;

            Env env = expr.Env;

            switch (from) {
                case ExprTypeKind.CHAR:
                    switch (to) {
                        case ExprTypeKind.SHORT:
                        case ExprTypeKind.USHORT:
                            return new TypeCast(TypeCastType.INT8_TO_INT16, expr, type);

                        case ExprTypeKind.LONG:
                        case ExprTypeKind.ULONG:
                            return new TypeCast(TypeCastType.INT8_TO_INT32, expr, type);

                        case ExprTypeKind.UCHAR:
                            return new TypeCast(TypeCastType.NOP, expr, type);

                        case ExprTypeKind.FLOAT:
                            // char -> long -> float
                            return new TypeCast(TypeCastType.INT32_TO_FLOAT, new TypeCast(TypeCastType.INT8_TO_INT32, expr, new TLong(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.DOUBLE:
                            // char -> long -> double
                            return new TypeCast(TypeCastType.INT32_TO_DOUBLE, new TypeCast(TypeCastType.INT8_TO_INT32, expr, new TLong(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.VOID:
                        case ExprTypeKind.POINTER:
                        case ExprTypeKind.FUNCTION:
                        case ExprTypeKind.ARRAY:
                        case ExprTypeKind.INCOMPLETE_ARRAY:
                        case ExprTypeKind.STRUCT_OR_UNION:
                        case ExprTypeKind.CHAR:
                        default:
                            throw new InvalidProgramException($"Cannot cast from {from} to {to}");
                    }

                case ExprTypeKind.SHORT:
                    switch (to) {
                        case ExprTypeKind.CHAR:
                        case ExprTypeKind.UCHAR:
                            return new TypeCast(TypeCastType.PRESERVE_INT8, expr, type);

                        case ExprTypeKind.USHORT:
                            return new TypeCast(TypeCastType.NOP, expr, type);

                        case ExprTypeKind.LONG:
                        case ExprTypeKind.ULONG:
                            return new TypeCast(TypeCastType.INT16_TO_INT32, expr, type);

                        case ExprTypeKind.FLOAT:
                            // short -> long -> float
                            return new TypeCast(TypeCastType.INT32_TO_FLOAT, new TypeCast(TypeCastType.INT16_TO_INT32, expr, new TLong(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.DOUBLE:
                            // short -> long -> double
                            return new TypeCast(TypeCastType.INT32_TO_DOUBLE, new TypeCast(TypeCastType.INT16_TO_INT32, expr, new TLong(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.VOID:
                        case ExprTypeKind.SHORT:
                        case ExprTypeKind.POINTER:
                        case ExprTypeKind.FUNCTION:
                        case ExprTypeKind.ARRAY:
                        case ExprTypeKind.INCOMPLETE_ARRAY:
                        case ExprTypeKind.STRUCT_OR_UNION:
                        default:
                            throw new InvalidProgramException($"Cannot cast from {from} to {to}");
                    }

                case ExprTypeKind.LONG:
                    switch (to) {
                        case ExprTypeKind.CHAR:
                            if (expr.IsConstExpr) {
                                return new ConstLong((SByte)((ConstLong)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT8, expr, type);

                        case ExprTypeKind.UCHAR:
                            if (expr.IsConstExpr) {
                                return new ConstULong((Byte)((ConstLong)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT8, expr, type);

                        case ExprTypeKind.SHORT:
                            if (expr.IsConstExpr) {
                                return new ConstLong((Int16)((ConstLong)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT16, expr, type);

                        case ExprTypeKind.USHORT:
                            if (expr.IsConstExpr) {
                                return new ConstULong((UInt16)((ConstLong)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT16, expr, type);

                        case ExprTypeKind.ULONG:
                            if (expr.IsConstExpr) {
                                return new ConstULong((UInt32)((ConstLong)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.NOP, expr, type);

                        case ExprTypeKind.FLOAT:
                            if (expr.IsConstExpr) {
                                return new ConstFloat(((ConstLong)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.INT32_TO_FLOAT, expr, type);

                        case ExprTypeKind.DOUBLE:
                            if (expr.IsConstExpr) {
                                return new ConstDouble(((ConstLong)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.INT32_TO_DOUBLE, expr, type);

                        case ExprTypeKind.VOID:
                        case ExprTypeKind.LONG:
                        case ExprTypeKind.POINTER:
                        case ExprTypeKind.FUNCTION:
                        case ExprTypeKind.ARRAY:
                        case ExprTypeKind.INCOMPLETE_ARRAY:
                        case ExprTypeKind.STRUCT_OR_UNION:
                        default:
                            throw new InvalidProgramException($"Cannot cast from {from} to {to}");
                    }

                default:
                    throw new InvalidProgramException();
            }
        }

        /// <summary>
        /// From:
        ///     uchar, ushort, ulong
        /// To:
        ///     char, uchar, short, ushort, long, ulong, float, double
        /// </summary>
        /// <remarks>
        /// Aaccording to MSDN "Conversions from Unsigned Integral Types",
        ///   unsigned long converts directly to double.
        /// However, I just treat unsigned long as long.
        /// </remarks>
        public static Expr UnsignedIntegralToArith(Expr expr, ExprType type) {
            ExprTypeKind from = expr.Type.Kind;
            ExprTypeKind to = type.Kind;

            Env env = expr.Env;

            switch (from) {
                case ExprTypeKind.UCHAR:
                    switch (to) {
                        case ExprTypeKind.CHAR:
                            return new TypeCast(TypeCastType.NOP, expr, type);

                        case ExprTypeKind.SHORT:
                        case ExprTypeKind.USHORT:
                            return new TypeCast(TypeCastType.UINT8_TO_UINT16, expr, type);

                        case ExprTypeKind.LONG:
                        case ExprTypeKind.ULONG:
                            return new TypeCast(TypeCastType.UINT8_TO_UINT32, expr, type);

                        case ExprTypeKind.FLOAT:
                            // uchar -> ulong -> long -> float
                            return new TypeCast(TypeCastType.INT32_TO_FLOAT, new TypeCast(TypeCastType.UINT8_TO_UINT32, expr, new TLong(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.DOUBLE:
                            // uchar -> ulong -> long -> double
                            return new TypeCast(TypeCastType.INT32_TO_DOUBLE, new TypeCast(TypeCastType.UINT8_TO_UINT32, expr, new TLong(type.IsConst, type.IsVolatile)), type);

                        default:
                            Debug.Assert(false);
                            return null;
                    }

                case ExprTypeKind.USHORT:
                    switch (to) {
                        case ExprTypeKind.CHAR:
                        case ExprTypeKind.UCHAR:
                            return new TypeCast(TypeCastType.PRESERVE_INT8, expr, type);

                        case ExprTypeKind.USHORT:
                            return new TypeCast(TypeCastType.NOP, expr, type);

                        case ExprTypeKind.LONG:
                        case ExprTypeKind.ULONG:
                            return new TypeCast(TypeCastType.UINT16_TO_UINT32, expr, type);

                        case ExprTypeKind.FLOAT:
                            // ushort -> ulong -> long -> float
                            return new TypeCast(TypeCastType.INT32_TO_FLOAT, new TypeCast(TypeCastType.UINT16_TO_UINT32, expr, new TLong(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.DOUBLE:
                            // ushort -> ulong -> long -> double
                            return new TypeCast(TypeCastType.INT32_TO_DOUBLE, new TypeCast(TypeCastType.UINT16_TO_UINT32, expr, new TLong(type.IsConst, type.IsVolatile)), type);

                        default:
                            Debug.Assert(false);
                            return null;
                    }

                case ExprTypeKind.ULONG:
                    switch (to) {
                        case ExprTypeKind.CHAR:
                            if (expr.IsConstExpr) {
                                return new ConstLong((SByte)((ConstULong)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT8, expr, type);

                        case ExprTypeKind.UCHAR:
                            if (expr.IsConstExpr) {
                                return new ConstULong((Byte)((ConstULong)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT8, expr, type);

                        case ExprTypeKind.SHORT:
                            if (expr.IsConstExpr) {
                                return new ConstLong((Int16)((ConstULong)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT16, expr, type);

                        case ExprTypeKind.USHORT:
                            if (expr.IsConstExpr) {
                                return new ConstULong((UInt16)((ConstULong)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT16, expr, type);

                        case ExprTypeKind.LONG:
                            if (expr.IsConstExpr) {
                                return new ConstLong((Int32)((ConstULong)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.NOP, expr, type);

                        case ExprTypeKind.FLOAT:
                            if (expr.IsConstExpr) {
                                return new ConstFloat(((ConstULong)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.INT32_TO_FLOAT, expr, type);

                        case ExprTypeKind.DOUBLE:
                            if (expr.IsConstExpr) {
                                return new ConstDouble(((ConstULong)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.INT32_TO_DOUBLE, expr, type);

                        default:
                            Debug.Assert(false);
                            return null;
                    }

                default:
                    Debug.Assert(false);
                    return null;
            }
        }

        /// <summary>
        /// From:
        ///     float, double
        /// To:
        ///     char, uchar, short, ushort, long, ulong, float, double
        /// </summary>
        /// <remarks>
        /// According to MSDN "Conversions from Floating-Point Types",
        ///   float cannot convert to unsigned char.
        /// I don't know why, but I follow it.
        /// </remarks>
        public static Expr FloatToArith(Expr expr, ExprType type) {

            ExprTypeKind from = expr.Type.Kind;
            ExprTypeKind to = type.Kind;
            Env env = expr.Env;

            switch (from) {
                case ExprTypeKind.FLOAT:
                    switch (to) {
                        case ExprTypeKind.CHAR:
                            if (expr.IsConstExpr) {
                                return new ConstLong((SByte)((ConstFloat)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT8, new TypeCast(TypeCastType.FLOAT_TO_INT32, expr, new TLong(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.SHORT:
                            if (expr.IsConstExpr) {
                                return new ConstLong((Int16)((ConstFloat)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT16, new TypeCast(TypeCastType.FLOAT_TO_INT32, expr, new TLong(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.USHORT:
                            if (expr.IsConstExpr) {
                                return new ConstULong((UInt16)((ConstFloat)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT16, new TypeCast(TypeCastType.FLOAT_TO_INT32, expr, new TLong(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.LONG:
                            if (expr.IsConstExpr) {
                                return new ConstLong((Int32)((ConstFloat)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.FLOAT_TO_INT32, expr, type);

                        case ExprTypeKind.ULONG:
                            if (expr.IsConstExpr) {
                                return new ConstULong((UInt32)((ConstFloat)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.FLOAT_TO_INT32, expr, type);

                        case ExprTypeKind.DOUBLE:
                            if (expr.IsConstExpr) {
                                return new ConstDouble(((ConstFloat)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.FLOAT_TO_DOUBLE, expr, type);

                        default:
                            throw new InvalidProgramException();
                    }

                case ExprTypeKind.DOUBLE:
                    switch (to) {
                        case ExprTypeKind.CHAR:
                            // double -> float -> char
                            if (expr.IsConstExpr) {
                                return new ConstLong((SByte)((ConstDouble)expr).value, env);
                            }
                            return FloatToArith(FloatToArith(expr, new TFloat(type.IsConst, type.IsVolatile)), new TChar(type.IsConst, type.IsVolatile));

                        case ExprTypeKind.SHORT:
                            // double -> float -> short
                            if (expr.IsConstExpr) {
                                return new ConstLong((Int16)((ConstDouble)expr).value, env);
                            }
                            return FloatToArith(FloatToArith(expr, new TFloat(type.IsConst, type.IsVolatile)), new TShort(type.IsConst, type.IsVolatile));

                        case ExprTypeKind.LONG:
                            // double -> float -> short
                            if (expr.IsConstExpr) {
                                return new ConstLong((Int32)((ConstDouble)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.DOUBLE_TO_INT32, expr, type);

                        case ExprTypeKind.ULONG:
                            if (expr.IsConstExpr) {
                                return new ConstULong((UInt32)((ConstDouble)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.DOUBLE_TO_INT32, expr, type);

                        case ExprTypeKind.USHORT:
                            // double -> long -> ushort
                            if (expr.IsConstExpr) {
                                return new ConstULong((UInt16)((ConstDouble)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.PRESERVE_INT16, new TypeCast(TypeCastType.DOUBLE_TO_INT32, expr, new TLong(type.IsConst, type.IsVolatile)), type);

                        case ExprTypeKind.FLOAT:
                            if (expr.IsConstExpr) {
                                return new ConstFloat((Single)((ConstDouble)expr).value, env);
                            }
                            return new TypeCast(TypeCastType.DOUBLE_TO_FLOAT, expr, type);

                        default:
                            throw new InvalidProgramException();
                    }

                default:
                    throw new InvalidProgramException();
            }
        }

        /// <summary>
        /// From:
        ///     pointer
        /// To:
        ///     pointer, integral
        /// </summary>
        public static Expr FromPointer(Expr expr, ExprType type, Env env) {
            ExprTypeKind from = expr.Type.Kind;
            ExprTypeKind to = type.Kind;

            if (from != ExprTypeKind.POINTER) {
                throw new InvalidOperationException("Expected a pointer.");
            }

            // if we are casting to another pointer, do a nop
            if (to == ExprTypeKind.POINTER) {
                if (expr.IsConstExpr) {
                    return new ConstPtr(((ConstPtr)expr).value, type, env);
                }
                return new TypeCast(TypeCastType.NOP, expr, type, env);
            }

            // if we are casting to an integral
            if (type.IsIntegral) {
                // pointer -> ulong -> whatever integral
                if (expr.IsConstExpr) {
                    expr = new ConstULong(((ConstPtr)expr).value, env);
                } else {
                    expr = new TypeCast(TypeCastType.NOP, expr, new TULong(type.IsConst, type.IsVolatile), env);
                }
                return MakeCast(expr, type, env);
            }

            throw new InvalidOperationException("Casting from a pointer to an unsupported type.");
        }

        /// <summary>
        /// From:
        ///     pointer, integral
        /// To:
        ///     pointer
        /// </summary>
        public static Expr ToPointer(Expr expr, ExprType type, Env env) {
            ExprTypeKind from = expr.Type.Kind;
            ExprTypeKind to = type.Kind;

            if (to != ExprTypeKind.POINTER) {
                throw new InvalidOperationException("Error: expected casting to pointer.");
            }

            if (from == ExprTypeKind.POINTER) {
                if (expr.IsConstExpr) {
                    return new ConstPtr(((ConstPtr)expr).value, type, env);
                }
                return new TypeCast(TypeCastType.NOP, expr, type, env);
            }

            if (expr.Type.IsIntegral) {
                // if we are casting from an integral

                // whatever integral -> ulong
                switch (expr.Type.Kind) {
                    case ExprTypeKind.CHAR:
                    case ExprTypeKind.SHORT:
                    case ExprTypeKind.LONG:
                        expr = SignedIntegralToArith(expr, new TULong(type.IsConst, type.IsVolatile));
                        break;
                    case ExprTypeKind.UCHAR:
                    case ExprTypeKind.USHORT:
                    case ExprTypeKind.ULONG:
                        expr = UnsignedIntegralToArith(expr, new TULong(type.IsConst, type.IsVolatile));
                        break;
                    default:
                        break;
                }

                // ulong -> pointer
                if (expr.IsConstExpr) {
                    return new ConstPtr(((ConstULong)expr).value, type, env);
                }
                return new TypeCast(TypeCastType.NOP, expr, type, env);
            }
            if (expr.Type is TFunction) {
                if (!expr.Type.EqualType((type as TPointer).RefType)) {
                    throw new InvalidOperationException("Casting from an incompatible function.");
                }

                // TODO: only allow compatible type?
                return new TypeCast(TypeCastType.NOP, expr, type, env);

            }
            if (expr.Type is TArray) {

                // TODO: allow any pointer type to cast to?
                return new TypeCast(TypeCastType.NOP, expr, type, env);
            }

            throw new InvalidOperationException("Error: casting from an unsupported type to pointer.");
        }

        // MakeCast
        // ========
        // input: Expr, type
        // output: TypeCast
        // converts Expr to type
        // 
        public static Expr MakeCast(Expr expr, ExprType type, Env env) {

            // if two types are equal, return Expr
            if (EqualType(expr.Type, type)) {
                return expr;
            }

            // from pointer
            if (expr.Type.Kind == ExprTypeKind.POINTER) {
                return FromPointer(expr, type, env);
            }

            // to pointer
            if (type.Kind == ExprTypeKind.POINTER) {
                return ToPointer(expr, type, env);
            }

            switch (expr.Type.Kind) {
                // from signed integral
                case ExprTypeKind.CHAR:
                case ExprTypeKind.SHORT:
                case ExprTypeKind.LONG:
                    return SignedIntegralToArith(expr, type);

                // from unsigned integral
                case ExprTypeKind.UCHAR:
                case ExprTypeKind.USHORT:
                case ExprTypeKind.ULONG:
                    return UnsignedIntegralToArith(expr, type);

                // from float
                case ExprTypeKind.FLOAT:
                case ExprTypeKind.DOUBLE:
                    return FloatToArith(expr, type);

                case ExprTypeKind.VOID:
                case ExprTypeKind.POINTER:
                case ExprTypeKind.FUNCTION:
                case ExprTypeKind.ARRAY:
                case ExprTypeKind.INCOMPLETE_ARRAY:
                case ExprTypeKind.STRUCT_OR_UNION:
                default:
                    throw new InvalidOperationException("Error: expression type not supported for casting from.");
            }

        }

        public static Expr MakeCast(Expr expr, ExprType type) =>
            MakeCast(expr, type, expr.Env);

        // UsualArithmeticConversion
        // =========================
        // input: e1, e2
        // output: tuple<e1', e2', enumexprtype>
        // performs the usual arithmetic conversion on e1 & e2
        // 
        // possible return type: double, float, ulong, long
        // 
        public static Tuple<Expr, Expr, ExprTypeKind> UsualArithmeticConversion(Expr e1, Expr e2) {
            ExprType t1 = e1.Type;
            ExprType t2 = e2.Type;

            Boolean c1 = t1.IsConst;
            Boolean v1 = t1.IsVolatile;
            Boolean c2 = t2.IsConst;
            Boolean v2 = t2.IsVolatile;
            // 1. if either Expr is double: both are converted to double
            if (t1.Kind == ExprTypeKind.DOUBLE || t2.Kind == ExprTypeKind.DOUBLE) {
                return new Tuple<Expr, Expr, ExprTypeKind>(MakeCast(e1, new TDouble(c1, v1)), MakeCast(e2, new TDouble(c2, v2)), ExprTypeKind.DOUBLE);
            }

            // 2. if either Expr is float: both are converted to float
            if (t1.Kind == ExprTypeKind.FLOAT || t2.Kind == ExprTypeKind.FLOAT) {
                return new Tuple<Expr, Expr, ExprTypeKind>(MakeCast(e1, new TFloat(c1, v1)), MakeCast(e2, new TFloat(c2, v2)), ExprTypeKind.FLOAT);
            }

            // 3. if either Expr is unsigned long: both are converted to unsigned long
            if (t1.Kind == ExprTypeKind.ULONG || t2.Kind == ExprTypeKind.ULONG) {
                return new Tuple<Expr, Expr, ExprTypeKind>(MakeCast(e1, new TULong(c1, v1)), MakeCast(e2, new TULong(c2, v2)), ExprTypeKind.ULONG);
            }

            // 4. both are converted to long
            return new Tuple<Expr, Expr, ExprTypeKind>(MakeCast(e1, new TLong(c1, v1)), MakeCast(e2, new TLong(c2, v2)), ExprTypeKind.LONG);

        }

        // UsualScalarConversion
        // =====================
        // input: e1, e2
        // output: tuple<e1', e2', enumexprtype>
        // first, convert pointers to ulongs, then do usual arithmetic conversion
        // 
        // possible return type: double, float, ulong, long
        // 
        public static Tuple<Expr, Expr, ExprTypeKind> UsualScalarConversion(Expr e1, Expr e2) {
            if (e1.Type.Kind == ExprTypeKind.POINTER) {
                e1 = FromPointer(e1, new TULong(e1.Type.IsConst, e1.Type.IsVolatile), e2.Env);
            }
            if (e2.Type.Kind == ExprTypeKind.POINTER) {
                e2 = FromPointer(e2, new TULong(e2.Type.IsConst, e2.Type.IsVolatile), e2.Env);
            }
            return UsualArithmeticConversion(e1, e2);
        }

        public static Tuple<Expr, ExprTypeKind> IntegralPromotion(Expr expr) {
            if (!expr.Type.IsIntegral) {
                throw new InvalidProgramException();
            }

            switch (expr.Type.Kind) {
                case ExprTypeKind.CHAR:
                case ExprTypeKind.SHORT:
                case ExprTypeKind.LONG:
                    return Tuple.Create(MakeCast(expr, new TLong(expr.Type.IsConst, expr.Type.IsVolatile)), ExprTypeKind.LONG);

                case ExprTypeKind.UCHAR:
                case ExprTypeKind.USHORT:
                case ExprTypeKind.ULONG:
                    return Tuple.Create(MakeCast(expr, new TULong(expr.Type.IsConst, expr.Type.IsVolatile)), ExprTypeKind.ULONG);

                case ExprTypeKind.VOID:
                case ExprTypeKind.FLOAT:
                case ExprTypeKind.DOUBLE:
                case ExprTypeKind.POINTER:
                case ExprTypeKind.FUNCTION:
                case ExprTypeKind.ARRAY:
                case ExprTypeKind.INCOMPLETE_ARRAY:
                case ExprTypeKind.STRUCT_OR_UNION:
                default:
                    throw new InvalidProgramException();
            }
        }

    }

    
}