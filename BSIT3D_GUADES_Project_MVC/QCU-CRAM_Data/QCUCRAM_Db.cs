using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using BSIT3D_GUADES_Project_MVC.QCUCRAM_Helpers;

namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Data
{
    /// <summary>
    /// The single place that knows how to talk to QCU-CRAM_DB.
    ///
    /// QCU-CRAM has no stored procedures (unlike the FTQuiz2 guide), so the mapping rule is:
    ///   * plain reads with no business rule  -> LINQ on Context, or Query&lt;T&gt; for shaped rows
    ///   * anything with a rule / a lock / a transaction -> parameterised SQL through this class
    ///
    /// Nothing outside QCU-CRAM_Data may construct a SqlConnection. Repositories that need a
    /// real transaction call InTransaction / RunInTransaction and receive the open connection.
    /// </summary>
    public class QCUCRAM_Db : IDisposable
    {
        private readonly QCUCRAMContext _context;

        public QCUCRAM_Db() : this(new QCUCRAMContext()) { }

        public QCUCRAM_Db(QCUCRAMContext context)
        {
            if (context == null) { throw new ArgumentNullException("context"); }
            _context = context;
        }

        public QCUCRAMContext Context { get { return _context; } }

        // ------------------------------------------------------------------
        //  Reads / writes through EF (no explicit transaction)
        // ------------------------------------------------------------------

        /// <summary>Runs a SELECT and materialises every row. Property names bind by column name.</summary>
        public List<T> Query<T>(string sql, params SqlParameter[] parameters)
        {
            return _context.Database.SqlQuery<T>(sql, ToObjects(parameters)).ToList();
        }

        /// <summary>Runs a SELECT that must return exactly one row (throws otherwise).</summary>
        public T QuerySingle<T>(string sql, params SqlParameter[] parameters)
        {
            return _context.Database.SqlQuery<T>(sql, ToObjects(parameters)).Single();
        }

        /// <summary>Runs a SELECT and returns the first row, or default(T) when there is none.</summary>
        public T QueryFirstOrDefault<T>(string sql, params SqlParameter[] parameters)
        {
            return _context.Database.SqlQuery<T>(sql, ToObjects(parameters)).FirstOrDefault();
        }

        /// <summary>Runs INSERT / UPDATE / DELETE and returns the rows affected.</summary>
        public int Execute(string sql, params SqlParameter[] parameters)
        {
            return _context.Database.ExecuteSqlCommand(sql, ToObjects(parameters));
        }

        /// <summary>SELECT COUNT(*) ... AS Total, returned as an int.</summary>
        public int Count(string sql, params SqlParameter[] parameters)
        {
            return QuerySingle<QCUCRAM_CountResult>(sql, parameters).Total;
        }

        /// <summary>True when the COUNT query returns anything above zero.</summary>
        public bool Any(string sql, params SqlParameter[] parameters)
        {
            return Count(sql, parameters) > 0;
        }

        // ------------------------------------------------------------------
        //  Raw connection (only for work that needs a real transaction)
        // ------------------------------------------------------------------

        /// <summary>Opens a connection on the QCU-CRAM connection string. Caller disposes it.</summary>
        public SqlConnection OpenConnection()
        {
            SqlConnection con = new SqlConnection(QCUCRAM_AppSettings.ConnectionString);
            con.Open();
            return con;
        }

        /// <summary>
        /// Opens a connection, starts a transaction at the given isolation level, runs the work
        /// and commits. Any exception rolls back and is re-thrown unchanged.
        /// </summary>
        public T InTransaction<T>(IsolationLevel level, Func<SqlConnection, SqlTransaction, T> work)
        {
            if (work == null) { throw new ArgumentNullException("work"); }

            using (SqlConnection con = OpenConnection())
            using (SqlTransaction tx = con.BeginTransaction(level))
            {
                try
                {
                    T result = work(con, tx);
                    tx.Commit();
                    return result;
                }
                catch
                {
                    SafeRollback(tx);
                    throw;
                }
            }
        }

        /// <summary>Same as InTransaction, for work that returns nothing.</summary>
        public void RunInTransaction(IsolationLevel level, Action<SqlConnection, SqlTransaction> work)
        {
            if (work == null) { throw new ArgumentNullException("work"); }

            InTransaction<bool>(level, (con, tx) =>
            {
                work(con, tx);
                return true;
            });
        }

        /// <summary>Rolls back without masking the original exception.</summary>
        public static void SafeRollback(SqlTransaction tx)
        {
            if (tx == null) { return; }
            try { tx.Rollback(); }
            catch { /* already finished or the connection died - nothing useful to do */ }
        }

        // ------------------------------------------------------------------
        //  Static command helpers (used inside transactions)
        // ------------------------------------------------------------------

        public static int Exec(SqlConnection con, SqlTransaction tx, string sql, params SqlParameter[] parameters)
        {
            using (SqlCommand cmd = new SqlCommand(sql, con, tx))
            {
                AddParameters(cmd, parameters);
                return cmd.ExecuteNonQuery();
            }
        }

        public static object Scalar(SqlConnection con, SqlTransaction tx, string sql, params SqlParameter[] parameters)
        {
            using (SqlCommand cmd = new SqlCommand(sql, con, tx))
            {
                AddParameters(cmd, parameters);
                return cmd.ExecuteScalar();
            }
        }

        public static int ScalarInt(SqlConnection con, SqlTransaction tx, string sql, params SqlParameter[] parameters)
        {
            object value = Scalar(con, tx, sql, parameters);
            return (value == null || value == DBNull.Value) ? 0 : Convert.ToInt32(value);
        }

        /// <summary>
        /// Self-opening overload. Creates and disposes its own connection, so callers that
        /// are NOT already inside a transaction do not have to construct a SqlConnection
        /// themselves. If a connection or transaction is already in scope, use the
        /// overload above instead so the work stays on that same connection.
        /// </summary>
        public static object Scalar(string sql, params SqlParameter[] parameters)
        {
            using (SqlConnection con = new SqlConnection(QCUCRAM_AppSettings.ConnectionString))
            {
                con.Open();
                return Scalar(con, null, sql, parameters);
            }
        }

        /// <summary>Self-opening counterpart of <see cref="ScalarInt(SqlConnection, SqlTransaction, string, SqlParameter[])"/>.</summary>
        public static int ScalarInt(string sql, params SqlParameter[] parameters)
        {
            object value = Scalar(sql, parameters);
            return (value == null || value == DBNull.Value) ? 0 : Convert.ToInt32(value);
        }

        public static DataTable Table(SqlConnection con, SqlTransaction tx, string sql, params SqlParameter[] parameters)
        {
            using (SqlCommand cmd = new SqlCommand(sql, con, tx))
            using (SqlDataAdapter da = new SqlDataAdapter(cmd))
            {
                AddParameters(cmd, parameters);
                DataTable dt = new DataTable();
                da.Fill(dt);
                return dt;
            }
        }

        /// <summary>Builds a parameter; null becomes DBNull so the column default / NULL is used.</summary>
        public static SqlParameter P(string name, object value)
        {
            return new SqlParameter(name, value ?? DBNull.Value);
        }

        /// <summary>Builds a parameter with an explicit SQL type (needed for DATE / TIME columns).</summary>
        public static SqlParameter P(string name, SqlDbType type, object value)
        {
            SqlParameter p = new SqlParameter(name, type);
            p.Value = value ?? DBNull.Value;
            return p;
        }

        private static void AddParameters(SqlCommand cmd, SqlParameter[] parameters)
        {
            if (parameters == null || parameters.Length == 0) { return; }
            foreach (SqlParameter p in parameters)
            {
                if (p == null) { continue; }
                cmd.Parameters.Add(p);
            }
        }

        /// <summary>
        /// EF's SqlQuery / ExecuteSqlCommand take object[], and understand SqlParameter inside it.
        ///
        /// IMPORTANT: every parameter is CLONED here. EF attaches the SqlParameter it is given to
        /// the command's SqlParameterCollection, and a SqlParameter may only ever belong to one
        /// collection. Callers routinely build one parameter and pass it to several queries (for
        /// example the admin dashboard's "today" parameter). Passing the caller's instance straight
        /// through made the second query throw
        ///   System.ArgumentException: The SqlParameter is already contained by another
        ///   SqlParameterCollection.
        /// Cloning keeps the caller's objects pristine and makes every call independent.
        /// </summary>
        private static object[] ToObjects(SqlParameter[] parameters)
        {
            if (parameters == null || parameters.Length == 0) { return new object[0]; }

            object[] copy = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                copy[i] = Clone(parameters[i]);
            }
            return copy;
        }

        /// <summary>Copies a SqlParameter so it can be attached to a fresh command.</summary>
        private static SqlParameter Clone(SqlParameter source)
        {
            if (source == null) { return null; }

            SqlParameter copy = new SqlParameter();
            copy.ParameterName = source.ParameterName;
            copy.SqlDbType = source.SqlDbType;
            copy.Direction = source.Direction;
            copy.IsNullable = source.IsNullable;
            copy.Value = source.Value;

            // Size / Precision / Scale only make sense for the fixed-width and decimal types;
            // copying them unconditionally is harmless but can upset some providers, so guard.
            if (source.Size > 0) { copy.Size = source.Size; }
            if (source.Precision > 0) { copy.Precision = source.Precision; }
            if (source.Scale > 0) { copy.Scale = source.Scale; }

            if (!string.IsNullOrEmpty(source.UdtTypeName)) { copy.UdtTypeName = source.UdtTypeName; }
            if (!string.IsNullOrEmpty(source.TypeName)) { copy.TypeName = source.TypeName; }

            return copy;
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
