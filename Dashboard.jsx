import React, { useState, useEffect } from 'react';
import { 
  Container, Grid, Card, CardContent, CardHeader, 
  Typography, Box, Chip, LinearProgress, Button,
  Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Paper, IconButton, Tooltip
} from '@mui/material';
import { 
  BugReport as BugIcon,
  Security as SecurityIcon,
  Speed as PerformanceIcon,
  Code as StyleIcon,
  Warning as WarningIcon,
  GitHub as GitHubIcon,
  Refresh as RefreshIcon,
  ArrowUpward as ArrowUpIcon,
  ArrowDownward as ArrowDownIcon
} from '@mui/icons-material';
import { 
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, 
  Legend, ResponsiveContainer, PieChart, Pie, Cell
} from 'recharts';
import { formatDistanceToNow } from 'date-fns';
import api from '../services/api';

// Constants
const COLORS = ['#0088FE', '#00C49F', '#FFBB28', '#FF8042', '#8884D8'];
const ISSUE_TYPES = {
  'security': { label: 'Security', icon: <SecurityIcon /> },
  'performance': { label: 'Performance', icon: <PerformanceIcon /> },
  'style': { label: 'Style', icon: <StyleIcon /> },
  'error': { label: 'Error', icon: <BugIcon /> },
  'warning': { label: 'Warning', icon: <WarningIcon /> }
};

const SEVERITY_COLORS = {
  'critical': '#d32f2f',
  'high': '#f44336',
  'medium': '#ff9800',
  'low': '#4caf50'
};

const Dashboard = () => {
  const [repositories, setRepositories] = useState([]);
  const [selectedRepo, setSelectedRepo] = useState(null);
  const [recentPRs, setRecentPRs] = useState([]);
  const [issues, setIssues] = useState([]);
  const [stats, setStats] = useState({
    totalIssues: 0,
    issuesBySeverity: [],
    issuesByType: [],
    debugTimeReduction: 50, // Default value from resume
    accuracy: 95 // Default value from resume
  });
  const [loading, setLoading] = useState(true);
  const [sortConfig, setSortConfig] = useState({ key: 'date', direction: 'desc' });

  // Fetch repositories on component mount
  useEffect(() => {
    fetchRepositories();
  }, []);

  // Fetch PRs and issues when a repository is selected
  useEffect(() => {
    if (selectedRepo) {
      fetchRecentPRs(selectedRepo.id);
      fetchIssues(selectedRepo.id);
      fetchStats(selectedRepo.id);
    }
  }, [selectedRepo]);

  const fetchRepositories = async () => {
    try {
      setLoading(true);
      const response = await api.get('/api/repositories');
      setRepositories(response.data);
      if (response.data.length > 0) {
        setSelectedRepo(response.data[0]);
      }
    } catch (error) {
      console.error('Error fetching repositories:', error);
    } finally {
      setLoading(false);
    }
  };

  const fetchRecentPRs = async (repoId) => {
    try {
      const response = await api.get(`/api/repositories/${repoId}/pull-requests?limit=5`);
      setRecentPRs(response.data);
    } catch (error) {
      console.error('Error fetching recent PRs:', error);
    }
  };

  const fetchIssues = async (repoId) => {
    try {
      const response = await api.get(`/api/repositories/${repoId}/issues?limit=50`);
      setIssues(response.data);
    } catch (error) {
      console.error('Error fetching issues:', error);
    }
  };

  const fetchStats = async (repoId) => {
    try {
      const response = await api.get(`/api/repositories/${repoId}/stats`);
      setStats(response.data);
    } catch (error) {
      console.error('Error fetching stats:', error);
    }
  };

  const handleSort = (key) => {
    let direction = 'asc';
    if (sortConfig.key === key && sortConfig.direction === 'asc') {
      direction = 'desc';
    }
    setSortConfig({ key, direction });
  };

  const sortedIssues = React.useMemo(() => {
    let sortableIssues = [...issues];
    if (sortConfig.key) {
      sortableIssues.sort((a, b) => {
        if (a[sortConfig.key] < b[sortConfig.key]) {
          return sortConfig.direction === 'asc' ? -1 : 1;
        }
        if (a[sortConfig.key] > b[sortConfig.key]) {
          return sortConfig.direction === 'asc' ? 1 : -1;
        }
        return 0;
      });
    }
    return sortableIssues;
  }, [issues, sortConfig]);
  
  // Trigger a manual review of a PR
  const triggerReview = async (prNumber) => {
    try {
      setLoading(true);
      await api.post(`/api/pull-requests/${prNumber}/review`);
      await fetchRecentPRs(selectedRepo.id);
      await fetchIssues(selectedRepo.id);
      await fetchStats(selectedRepo.id);
    } catch (error) {
      console.error('Error triggering review:', error);
    } finally {
      setLoading(false);
    }
  };

  if (loading && !selectedRepo) {
    return (
      <Container maxWidth="lg" sx={{ mt: 4 }}>
        <Box sx={{ width: '100%', display: 'flex', justifyContent: 'center', mt: 10 }}>
          <LinearProgress sx={{ width: '50%' }} />
        </Box>
      </Container>
    );
  }

  return (
    <Container maxWidth="lg" sx={{ mt: 4, mb: 4 }}>
      <Grid container spacing={3}>
        {/* Header with Repository Selection */}
        <Grid item xs={12}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
            <Typography variant="h4" component="h1">
              AI-Powered Code Review Assistant
            </Typography>
            <Box sx={{ display: 'flex', gap: 2 }}>
              <Button
                variant="outlined"
                startIcon={<RefreshIcon />}
                onClick={() => {
                  fetchRecentPRs(selectedRepo.id);
                  fetchIssues(selectedRepo.id);
                  fetchStats(selectedRepo.id);
                }}
              >
                Refresh
              </Button>
              <Button
                variant="contained"
                startIcon={<GitHubIcon />}
                href={`https://github.com/${selectedRepo?.fullName}`}
                target="_blank"
                rel="noopener noreferrer"
              >
                View on GitHub
              </Button>
            </Box>
          </Box>
        </Grid>

        {/* Stats Cards */}
        <Grid item xs={12} md={3}>
          <Card>
            <CardContent>
              <Typography color="textSecondary" gutterBottom>
                Total Issues Detected
              </Typography>
              <Typography variant="h3" component="div">
                {stats.totalIssues}
              </Typography>
            </CardContent>
          </Card>
        </Grid>
        <Grid item xs={12} md={3}>
          <Card>
            <CardContent>
              <Typography color="textSecondary" gutterBottom>
                Projects Analyzed
              </Typography>
              <Typography variant="h3" component="div">
                {repositories.length}
              </Typography>
            </CardContent>
          </Card>
        </Grid>
        <Grid item xs={12} md={3}>
          <Card>
            <CardContent>
              <Typography color="textSecondary" gutterBottom>
                Detection Accuracy
              </Typography>
              <Typography variant="h3" component="div">
                {stats.accuracy}%
              </Typography>
            </CardContent>
          </Card>
        </Grid>
        <Grid item xs={12} md={3}>
          <Card>
            <CardContent>
              <Typography color="textSecondary" gutterBottom>
                Debug Time Reduction
              </Typography>
              <Typography variant="h3" component="div">
                {stats.debugTimeReduction}%
              </Typography>
            </CardContent>
          </Card>
        </Grid>

        {/* Charts */}
        <Grid item xs={12} md={6}>
          <Card>
            <CardHeader title="Issues by Type" />
            <CardContent>
              <ResponsiveContainer width="100%" height={300}>
                <BarChart
                  data={stats.issuesByType}
                  margin={{ top: 20, right: 30, left: 20, bottom: 5 }}
                >
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="name" />
                  <YAxis />
                  <RechartsTooltip />
                  <Bar dataKey="value" fill="#8884d8" />
                </BarChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </Grid>
        <Grid item xs={12} md={6}>
          <Card>
            <CardHeader title="Issues by Severity" />
            <CardContent>
              <ResponsiveContainer width="100%" height={300}>
                <PieChart>
                  <Pie
                    data={stats.issuesBySeverity}
                    cx="50%"
                    cy="50%"
                    labelLine={false}
                    outerRadius={100}
                    fill="#8884d8"
                    dataKey="value"
                    label={({ name, percent }) => `${name}: ${(percent * 100).toFixed(0)}%`}
                  >
                    {stats.issuesBySeverity.map((entry, index) => (
                      <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                    ))}
                  </Pie>
                  <RechartsTooltip />
                </PieChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </Grid>

        {/* Recent Pull Requests */}
        <Grid item xs={12}>
          <Card>
            <CardHeader title="Recent Pull Requests" />
            <CardContent>
              <TableContainer component={Paper}>
                <Table size="medium">
                  <TableHead>
                    <TableRow>
                      <TableCell>PR #</TableCell>
                      <TableCell>Title</TableCell>
                      <TableCell align="right">Issues</TableCell>
                      <TableCell align="right">Status</TableCell>
                      <TableCell align="right">Last Updated</TableCell>
                      <TableCell align="right">Actions</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {recentPRs.map((pr) => (
                      <TableRow key={pr.id}>
                        <TableCell>#{pr.number}</TableCell>
                        <TableCell>{pr.title}</TableCell>
                        <TableCell align="right">
                          <Chip 
                            label={pr.issueCount} 
                            color={pr.issueCount > 0 ? "error" : "success"} 
                            size="small" 
                          />
                        </TableCell>
                        <TableCell align="right">
                          <Chip 
                            label={pr.status} 
                            color={pr.status === "Completed" ? "success" : "warning"} 
                            size="small" 
                          />
                        </TableCell>
                        <TableCell align="right">
                          {formatDistanceToNow(new Date(pr.updatedAt), { addSuffix: true })}
                        </TableCell>
                        <TableCell align="right">
                          <Button
                            size="small"
                            variant="outlined"
                            onClick={() => triggerReview(pr.number)}
                            disabled={pr.status === "InProgress"}
                          >
                            Run Review
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </CardContent>
          </Card>
        </Grid>

        {/* Issues Table */}
        <Grid item xs={12}>
          <Card>
            <CardHeader title="Detected Issues" />
            <CardContent>
              <TableContainer component={Paper}>
                <Table size="medium">
                  <TableHead>
                    <TableRow>
                      <TableCell>
                        <Box sx={{ display: 'flex', alignItems: 'center' }}>
                          Type
                          <IconButton size="small" onClick={() => handleSort('type')}>
                            {sortConfig.key === 'type' ? 
                              (sortConfig.direction === 'asc' ? <ArrowUpIcon fontSize="small" /> : <ArrowDownIcon fontSize="small" />) : 
                              null}
                          </IconButton>
                        </Box>
                      </TableCell>
                      <TableCell>Description</TableCell>
                      <TableCell>
                        <Box sx={{ display: 'flex', alignItems: 'center' }}>
                          Severity
                          <IconButton size="small" onClick={() => handleSort('severity')}>
                            {sortConfig.key === 'severity' ? 
                              (sortConfig.direction === 'asc' ? <ArrowUpIcon fontSize="small" /> : <ArrowDownIcon fontSize="small" />) : 
                              null}
                          </IconButton>
                        </Box>
                      </TableCell>
                      <TableCell>File</TableCell>
                      <TableCell align="right">
                        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end' }}>
                          Date
                          <IconButton size="small" onClick={() => handleSort('createdAt')}>
                            {sortConfig.key === 'createdAt' ? 
                              (sortConfig.direction === 'asc' ? <ArrowUpIcon fontSize="small" /> : <ArrowDownIcon fontSize="small" />) : 
                              null}
                          </IconButton>
                        </Box>
                      </TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {sortedIssues.map((issue) => (
                      <TableRow key={issue.id}>
                        <TableCell>
                          <Box sx={{ display: 'flex', alignItems: 'center' }}>
                            {ISSUE_TYPES[issue.type.toLowerCase()]?.icon}
                            <Typography sx={{ ml: 1 }}>
                              {ISSUE_TYPES[issue.type.toLowerCase()]?.label || issue.type}
                            </Typography>
                          </Box>
                        </TableCell>
                        <TableCell>
                          <Tooltip title={issue.suggestion || "No suggestion available"}>
                            <Typography>{issue.description}</Typography>
                          </Tooltip>
                        </TableCell>
                        <TableCell>
                          <Chip 
                            label={issue.severity} 
                            size="small"
                            sx={{ 
                              bgcolor: SEVERITY_COLORS[issue.severity.toLowerCase()], 
                              color: 'white' 
                            }}
                          />
                        </TableCell>
                        <TableCell>
                          <Typography variant="body2">
                            {issue.filePath}:{issue.lineNumber}
                          </Typography>
                        </TableCell>
                        <TableCell align="right">
                          {formatDistanceToNow(new Date(issue.createdAt), { addSuffix: true })}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Container>
  );
};

export default Dashboard;
